using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Infrastructure.Documents;

public sealed class AssistenteDocumentWorker(IServiceScopeFactory scopes,
    IOptions<AssistenteDocumentSettings> options, ILogger<AssistenteDocumentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<AssistenteDocumentProcessor>();
                if (await processor.ProcessNextAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Falha operacional na fila documental; nova consulta será realizada."); }
            try { await Task.Delay(1500, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

public sealed class AssistenteDocumentProcessor(JuridicoAiDbContext db, IFileStorage storage,
    LocalDocumentExtractor extractor, IOptions<AssistenteDocumentSettings> options)
{
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Enabled) return false;
        AssistenteDocumento? source; var fence = Guid.NewGuid(); var now = DateTime.UtcNow;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            source = await db.AssistenteDocumentos.FromSqlInterpolated($"""
                SELECT * FROM ai_documents WHERE estado = 'Recebido' OR (estado = 'Processando' AND lease_ate < {now})
                ORDER BY criado_em LIMIT 1 FOR UPDATE SKIP LOCKED
                """).SingleOrDefaultAsync(ct);
            if (source is null) { await tx.CommitAsync(ct); return false; }
            if (source.Tentativas >= settings.MaxAttempts)
            {
                source.Estado = "Falhou"; source.CodigoErro = "AI_DOCUMENT_RETRY_LIMIT";
                source.LeaseToken = null; source.LeaseAte = null;
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
            }
            source.Estado = "Processando"; source.LeaseToken = fence; source.LeaseAte = now.AddSeconds(settings.LeaseSeconds);
            source.Tentativas++; source.PaginasConcluidas = 0; source.PaginasTotal = null; source.CodigoErro = null;
            await db.AssistenteDocumentoTrechos.Where(c => c.DocumentoId == source.Id).ExecuteDeleteAsync(ct);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        var dir = Path.Combine(Path.GetTempPath(), "juridicoai-extract", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var path = Path.Combine(dir, "source." + (source.Formato is "pdf" or "docx" or "txt" or "png" or "jpg" or "jpeg" ? source.Formato : "bin"));
        var ordinal = 0; var lastPage = 0; var rejected = false; var trusted = false;
        try
        {
            await using (var input = await storage.GetAsync(source.StorageId, ct))
            await using (var output = File.Create(path))
            {
                var buffer = new byte[81920]; int count; long length = 0;
                while ((count = await input.ReadAsync(buffer, ct)) > 0)
                {
                    length += count; if (length > settings.MaxFileBytes) throw DocumentProcess.Error("AI_UPLOAD_TOO_LARGE");
                    await output.WriteAsync(buffer.AsMemory(0, count), ct);
                }
            }
            await using (var input = File.OpenRead(path))
            {
                var sha = Convert.ToHexStringLower(await SHA256.HashDataAsync(input, ct));
                if (source.Sha256 is not null && source.Sha256 != sha) throw DocumentProcess.Error("AI_DOCUMENT_SOURCE_CHANGED");
                source.Sha256 = sha;
            }
            source.QualidadeVersao = DocumentQuality.VersionFor(settings);
            await foreach (var part in extractor.ExtractAsync(path, source.Formato, ct))
            {
                if (!await OwnLeaseAsync(source.Id, fence, ct)) return true;
                if (part.Page is { } page)
                { source.PaginasTotal = part.TotalPages ?? Math.Max(page, source.PaginasTotal ?? 0); if (page > lastPage) source.PaginasConcluidas = lastPage; lastPage = page; }
                var text = part.Text;
                db.Add(new AssistenteDocumentoTrecho { Id = Guid.NewGuid(), OrganizationId = source.OrganizationId,
                    DocumentoId = source.Id, Ordem = ordinal++, Pagina = part.Page, Localizador = part.Locator,
                    Texto = text, Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))),
                    Qualidade = part.Quality, Motivo = part.Reason, Metricas = JsonDocument.Parse(part.Metrics) });
                rejected |= part.Quality != "Confiavel"; trusted |= part.Quality == "Confiavel";
                source.LeaseAte = DateTime.UtcNow.AddSeconds(settings.LeaseSeconds);
                await SaveFencedAsync(source, fence, ct);
            }
            if (!await OwnLeaseAsync(source.Id, fence, ct)) return true;
            source.Estado = !trusted ? "Inutilizavel" : rejected ? "Parcial" : "Concluido";
            source.PaginasConcluidas = lastPage;
            source.CodigoErro = ordinal == 0 ? "NO_READABLE_TEXT" : null;
            source.LeaseAte = null;
            await SaveFencedAsync(source, fence, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Leave the durable lease for another worker; no partial data is eligible.
            throw;
        }
        catch (Exception ex)
        {
            db.ChangeTracker.Clear();
            var code = ex is AssistenteIaException ai ? ai.Code : ex is DecoderFallbackException ? "AI_DOCUMENT_ENCODING_INVALID" : "AI_DOCUMENT_PARSE_FAILED";
            var state = trusted ? "Parcial" : "Falhou";
            await db.AssistenteDocumentos.Where(d => d.Id == source.Id && d.LeaseToken == fence && d.LeaseAte > DateTime.UtcNow && d.Estado == "Processando").ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Estado, state).SetProperty(d => d.CodigoErro, code)
                .SetProperty(d => d.LeaseAte, (DateTime?)null), CancellationToken.None);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        return true;
    }
    private Task<bool> OwnLeaseAsync(Guid id, Guid fence, CancellationToken ct) =>
        db.AssistenteDocumentos.AsNoTracking().AnyAsync(d => d.Id == id && d.LeaseToken == fence && d.Estado == "Processando" && d.LeaseAte > DateTime.UtcNow, ct);
    private async Task SaveFencedAsync(AssistenteDocumento source, Guid fence, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var own = await db.AssistenteDocumentos.FromSqlInterpolated($"SELECT * FROM ai_documents WHERE id = {source.Id} AND lease_token = {fence} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
        if (own is null || own.Estado != "Processando" || own.LeaseAte <= DateTime.UtcNow)
            throw new InvalidOperationException("Lease documental substituído.");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
