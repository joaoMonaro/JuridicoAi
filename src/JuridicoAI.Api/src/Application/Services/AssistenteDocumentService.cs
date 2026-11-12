using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Settings;
using JuridicoAI.Api.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Application.Services;

public sealed class AssistenteDocumentService(JuridicoAiDbContext db, IAssistenteWorkspaceRepository workspace,
    IPecaRepository pecas, IFileStorage storage, IAuthContext auth, IAssistenteFeatureAccess features,
    IAssistenteTransparencyService transparency, IOptions<AssistenteDocumentSettings> options)
{
    private AssistenteDocumentSettings Settings => options.Value;
    public bool Enabled => Settings.Enabled;
    public AssistenteDocumentSettings RetrievalSettings => Settings;
    public async Task AuthorizeRetrievalAsync(Guid conversationId, CancellationToken ct) => await AuthorizeAsync(conversationId, true, ct);
    public async Task SynchronizeAsync(Guid conversationId, CancellationToken ct)
    {
        if (!Settings.Enabled) return;
        var (_, piece) = await AuthorizeAsync(conversationId, true, ct);
        await EnsureCaseSourcesAsync(piece.CasoId, ct);
        await RequeueOutdatedAsync(conversationId, piece.CasoId, ct);
    }
    public async Task<AssistenteDocumentoDto> UploadAsync(Guid conversaId, Guid uploadId, string name,
        Stream input, CancellationToken ct)
    {
        await AuthorizeAsync(conversaId, true, ct);
        if (uploadId == Guid.Empty) throw new AssistenteIaException(400, "AI_UPLOAD_ID_REQUIRED", "Identificador de envio obrigatório.");
        name = Path.GetFileName(name.Replace('\\', '/')).Trim();
        if (name.Length is 0 or > 255) throw new AssistenteIaException(400, "AI_DOCUMENT_NAME_INVALID", "Nome de arquivo inválido.");
        var format = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
        if (format is not ("txt" or "pdf" or "docx" or "png" or "jpg" or "jpeg"))
            throw new AssistenteIaException(415, "AI_DOCUMENT_FORMAT_UNSUPPORTED", "Envie PDF, DOCX, TXT, PNG ou JPEG.");
        // Count bytes while reading, even for chunked requests. Validate magic
        // independently of the supplied MIME or filename before persistence.
        using var bytes = new MemoryStream(); var buffer = new byte[81920]; int count;
        while ((count = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (bytes.Length + count > Settings.MaxFileBytes) throw Limit();
            await bytes.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        if (bytes.Length == 0 || !ValidSignature(bytes.GetBuffer().AsSpan(0, (int)bytes.Length), format))
            throw new AssistenteIaException(422, "AI_DOCUMENT_FORMAT_MISMATCH", "O conteúdo não corresponde ao formato informado.");
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes.GetBuffer().AsSpan(0, (int)bytes.Length)));
        string? createdStorage = null;
        try
        {
            return await workspace.EmTransacaoAsync(async token =>
            {
                var conversation = await workspace.ObterConversaAsync(conversaId, auth.OrganizationId, auth.UserId, true, token) ?? throw Missing();
                var piece = await pecas.ObterPecaAsync(conversation.PecaId, auth.OrganizationId, token) ?? throw Missing();
                var existing = await db.AssistenteDocumentos.SingleOrDefaultAsync(d => d.ConversaId == conversaId && d.ClientUploadId == uploadId, token);
                if (existing is not null)
                {
                    if (existing.Sha256 != sha || existing.Nome != name) throw new AssistenteIaException(409, "AI_IDEMPOTENCY_MISMATCH", "Este envio já foi usado para outro arquivo.");
                    return await DtoAsync(existing, token);
                }
                var items = await db.AssistenteDocumentos.Where(d => d.ConversaId == conversaId).Select(d => d.TamanhoBytes).ToArrayAsync(token);
                if (items.Length >= Settings.MaxAttachments || items.Sum() + bytes.Length > Settings.MaxConversationBytes) throw Limit();
                var id = Guid.NewGuid(); var storageId = $"assistant/{auth.OrganizationId:N}/{conversaId:N}/{id:N}.{format}";
                bytes.Position = 0; await storage.SaveAsync(storageId, bytes, token); createdStorage = storageId;
                var source = new AssistenteDocumento { Id = id, OrganizationId = auth.OrganizationId, ConversaId = conversaId,
                    CasoId = piece.CasoId, ClientUploadId = uploadId, Nome = name, Formato = format, StorageId = storageId,
                    TamanhoBytes = bytes.Length, Sha256 = sha };
                db.Add(source);
                return await DtoAsync(source, token);
            }, ct);
        }
        catch
        {
            // A commit acknowledgement can be lost. Only remove a file after
            // positively confirming it has no committed metadata reference.
            if (createdStorage is not null)
            {
                try
                {
                    if (!await db.AssistenteDocumentos.AsNoTracking().AnyAsync(d => d.StorageId == createdStorage, CancellationToken.None))
                        await storage.DeleteAsync(createdStorage, CancellationToken.None);
                }
                catch (Exception) { /* Preserve the original when outcome is uncertain. */ }
            }
            throw;
        }
    }
    public async Task<IReadOnlyList<AssistenteDocumentoDto>> ListAsync(Guid conversaId, CancellationToken ct)
    {
        var (_, piece) = await AuthorizeAsync(conversaId, false, ct);
        if (Settings.Enabled)
        {
            await features.ExigirModoAsync(auth.OrganizationId, AssistenteModo.Perguntar, ct);
            await transparency.ExigirConfirmacaoAsync(ct);
            await EnsureCaseSourcesAsync(piece.CasoId, ct);
            await RequeueOutdatedAsync(conversaId, piece.CasoId, ct);
        }
        var sources = await EligibleSources(conversaId, piece.CasoId).OrderBy(d => d.CriadoEm).Take(251).ToArrayAsync(ct);
        if (sources.Length > 250) throw new AssistenteIaException(413, "AI_DOCUMENT_SOURCE_LIMIT", "O conjunto de documentos excede o limite desta etapa.");
        var result = new List<AssistenteDocumentoDto>();
        foreach (var source in sources) result.Add(await DtoAsync(source, ct));
        return result;
    }
    public async Task<(Stream Stream, string Name, string ContentType)> DownloadAsync(Guid conversaId, Guid id, CancellationToken ct)
    {
        var (_, piece) = await AuthorizeAsync(conversaId, false, ct);
        var source = await EligibleSources(conversaId, piece.CasoId).SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw Missing();
        return (await storage.GetAsync(source.StorageId, ct), source.Nome, source.Formato switch
        { "pdf" => "application/pdf", "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "txt" => "text/plain", "png" => "image/png", "jpg" or "jpeg" => "image/jpeg", _ => "application/octet-stream" });
    }
    public async Task<AssistenteDocumentoDto> GetAsync(Guid conversaId, Guid id, CancellationToken ct)
    {
        var (_, piece) = await AuthorizeAsync(conversaId, false, ct);
        return await DtoAsync(await EligibleSources(conversaId, piece.CasoId).SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw Missing(), ct);
    }
    public async Task<IReadOnlyList<AssistenteDocumentoBuscaResultado>> SearchAsync(Guid conversaId, string query, int limit, CancellationToken ct,
        IReadOnlyList<Guid>? snapshotIds = null)
    {
        var (_, piece) = await AuthorizeAsync(conversaId, true, ct);
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200 || limit is < 1 or > 12)
            throw new AssistenteIaException(400, "AI_DOCUMENT_SEARCH_INVALID", "Informe uma consulta de até 200 caracteres e limite de 1 a 12.");
        // Filter ownership, live Case access, binding and quality BEFORE ranking
        // and limiting. Rejected text has an empty search vector as a second gate.
        var sources = EligibleSources(conversaId, piece.CasoId).Where(d => (d.ConversaId == null || d.Vinculado) &&
            (d.Estado == "Concluido" || d.Estado == "Parcial"));
        if (snapshotIds is not null) sources = sources.Where(d => snapshotIds.Contains(d.Id));
        var term = query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return await (from chunk in db.AssistenteDocumentoTrechos.AsNoTracking()
            join source in sources on chunk.DocumentoId equals source.Id
            where chunk.Qualidade == "Confiavel" && (chunk.Busca.Matches(EF.Functions.PlainToTsQuery("portuguese", query)) ||
                EF.Functions.ILike(chunk.Texto, "%" + term + "%", "\\"))
            orderby chunk.Busca.Rank(EF.Functions.PlainToTsQuery("portuguese", query)) descending, source.Id, chunk.Ordem
            select new AssistenteDocumentoBuscaResultado(source.Id, source.Nome, source.ConversaId.HasValue ? "AnexoPrivado" : "DocumentoCaso",
                chunk.Id, chunk.Pagina, chunk.Localizador, chunk.Texto, chunk.Ordem)).Take(limit).ToArrayAsync(ct);
    }
    public async Task<IReadOnlyList<AssistenteDocumentoBuscaResultado>> ReadRangeAsync(Guid conversaId, Guid sourceId, int after,
        int limit, CancellationToken ct, int? page = null)
    {
        var (_, piece) = await AuthorizeAsync(conversaId, true, ct);
        if (after < -1 || limit is < 1 or > 12 || page is < 1) throw new AssistenteIaException(400, "AI_DOCUMENT_RANGE_INVALID", "Faixa de leitura inválida.");
        var source = await EligibleSources(conversaId, piece.CasoId).SingleOrDefaultAsync(d => d.Id == sourceId, ct) ?? throw Missing();
        return await db.AssistenteDocumentoTrechos.AsNoTracking().Where(c => c.DocumentoId == source.Id && c.Qualidade == "Confiavel" && c.Ordem > after && (!page.HasValue || c.Pagina == page))
            .OrderBy(c => c.Ordem).Take(limit).Select(c => new AssistenteDocumentoBuscaResultado(source.Id, source.Nome,
                source.ConversaId.HasValue ? "AnexoPrivado" : "DocumentoCaso", c.Id, c.Pagina, c.Localizador, c.Texto, c.Ordem)).ToArrayAsync(ct);
    }
    public async Task SaveCoverageAsync(Guid conversaId, Guid responseId, AssistenteCoberturaDocumentalDto coverage, CancellationToken ct)
    {
        await AuthorizeAsync(conversaId, true, ct);
        var json = AssistentePatchCompiler.Json(coverage);
        var changed = await db.AssistenteMensagens.Where(m => m.Id == responseId && m.ConversaId == conversaId &&
            m.OrganizationId == auth.OrganizationId && m.Estado == "Gerando" && m.ExpiraEm > DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.CoberturaDocumental, json), ct);
        if (changed != 1) throw new AssistenteIaException(409, "AI_REQUEST_INTERRUPTED", "A consulta documental foi interrompida.");
    }
    public async Task RetryAsync(Guid conversaId, Guid id, CancellationToken ct)
    {
        var (_, piece) = await AuthorizeAsync(conversaId, true, ct);
        var source = await EligibleSources(conversaId, piece.CasoId).SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw Missing();
        if (source.Estado == "Processando" || source.Estado == "Recebido") return;
        await db.AssistenteDocumentos.Where(d => d.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.Estado, "Recebido").SetProperty(d => d.Tentativas, 0)
            .SetProperty(d => d.CodigoErro, (string?)null).SetProperty(d => d.LeaseToken, (Guid?)null).SetProperty(d => d.LeaseAte, (DateTime?)null), ct);
    }
    public async Task<AssistenteDocumentoContexto> ContextAsync(AssistenteConversa conversation, Peca piece,
        IReadOnlyList<Guid>? attachmentIds, CancellationToken ct, bool usarRecuperacao = false, IReadOnlyList<Guid>? fontesFixadas = null)
    {
        var retrieval = Settings.RetrievalEnabled && usarRecuperacao;
        var ids = attachmentIds?.Distinct().ToArray() ?? [];
        if (!Settings.Enabled)
        {
            if (ids.Length > 0) throw new AssistenteIaException(503, "AI_DOCUMENTS_DISABLED", "A leitura documental não está habilitada.");
            return new("", "[]", []);
        }
        if (ids.Length > Settings.MaxAttachments) throw Limit();
        var sources = await EligibleSources(conversation.Id, piece.CasoId)
            .Where(d => (fontesFixadas == null || fontesFixadas.Contains(d.Id)) && (d.ConversaId == null || d.Vinculado || ids.Contains(d.Id))).OrderBy(d => d.CriadoEm).Take(251).ToArrayAsync(ct);
        if (fontesFixadas is not null && sources.Length != fontesFixadas.Count) throw new AssistenteIaException(409, "AI_DOCUMENT_SOURCE_CHANGED", "O conjunto documental mudou após o pedido.");
        if (sources.Length > 250) throw new AssistenteIaException(413, "AI_DOCUMENT_SOURCE_LIMIT", "O conjunto de documentos excede o limite desta etapa.");
        if (ids.Any(id => !sources.Any(d => d.Id == id && d.ConversaId == conversation.Id))) throw Missing();
        if (sources.Any(d => d.Estado is "Recebido" or "Processando"))
            throw new AssistenteIaException(409, "AI_DOCUMENTS_PROCESSING", "Há documentos em processamento. Aguarde a leitura antes de enviar o pedido.", true);
        var result = new List<object>(); var manifests = new List<object>(); var alerts = new List<AssistenteAlertaDto>();
        var characters = 0; var retrievalSources = new List<AssistenteFonteRecuperacao>();
        foreach (var source in sources)
        {
            var chunks = await db.AssistenteDocumentoTrechos.AsNoTracking().Where(c => c.DocumentoId == source.Id).OrderBy(c => c.Ordem)
                .Select(c => new { c.Id, c.Qualidade, c.Pagina, c.Localizador, c.Motivo, Texto = retrieval ? "" : c.Texto }).ToArrayAsync(ct);
            var usable = chunks.Where(c => c.Qualidade == "Confiavel").ToArray();
            var incomplete = source.Estado != "Concluido" || chunks.Any(c => c.Qualidade != "Confiavel");
            if (incomplete) alerts.Add(new("DOCUMENT_READING_INCOMPLETE", $"{source.Nome}: leitura incompleta ou duvidosa; somente os trechos confiáveis foram utilizados."));
            manifests.Add(new { id = source.Id, hash = source.Sha256, extractor = source.ExtratorVersao,
                quality = source.QualidadeVersao, revision = source.LeaseToken, utilized = usable.Length > 0, incomplete, source.DocumentoCasoId, source.StorageId });
            if (retrieval)
            {
                var origin = source.ConversaId.HasValue ? "AnexoPrivado" : "DocumentoCaso";
                retrievalSources.Add(new(source.Id, source.Nome, origin, usable.Length, chunks.Length - usable.Length, source.Estado));
                result.Add(new { id = source.Id, nome = source.Nome, origem = origin, trechosElegiveis = usable.Length,
                    trechosExcluidos = chunks.Length - usable.Length, estado = source.Estado, erro = source.CodigoErro });
            }
            else result.Add(new { id = source.Id, nome = source.Nome, origem = source.ConversaId.HasValue ? "AnexoPrivado" : "DocumentoCaso",
                sha256 = source.Sha256, cobertura = incomplete ? "ParcialOuIndisponivel" : "IntegralDaExtracao",
                erro = source.CodigoErro, trechos = usable.Select(c => new { id = c.Id, pagina = c.Pagina, localizador = c.Localizador, texto = c.Texto }),
                excluidos = chunks.Where(c => c.Qualidade != "Confiavel").Select(c => new { pagina = c.Pagina, localizador = c.Localizador, motivo = c.Motivo }) });
            characters += usable.Sum(c => c.Texto.Length);
            // Full coverage for this initial adapter. Fail explicitly instead of
            // silently taking top-k when a user asks to consider all documents.
            if (characters > 60_000) throw new AssistenteIaException(413, "AI_DOCUMENT_CONTEXT_TOO_LARGE", "Os documentos permanecem salvos, mas excedem a janela de leitura integral desta etapa. Nenhum pedido foi enviado à IA.");
        }
        if (ids.Length > 0) await db.AssistenteDocumentos.Where(d => d.ConversaId == conversation.Id && ids.Contains(d.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Vinculado, true), ct);
        var tag = retrieval ? "CATALOGO_FONTES_DOCUMENTAIS_JSON" : "FONTES_DOCUMENTAIS_JSON";
        return new(sources.Length == 0 ? "" : $"\n[{tag} dado_nao_confiavel=\"true\"]\n" + JsonSerializer.Serialize(result) + $"\n[/{tag}]\n",
            JsonSerializer.Serialize(manifests), alerts, retrieval && sources.Length > 0 ? retrievalSources : null);
    }
    public async Task<AssistenteSourceSnapshot[]> CaptureSourcesAsync(Guid conversationId, IReadOnlyList<Guid>? attachments, CancellationToken ct)
    {
        if (!Enabled)
        {
            if (attachments?.Count > 0) throw new AssistenteIaException(503, "AI_DOCUMENTS_DISABLED", "Documentos não estão habilitados.");
            return [];
        }
        var (_, piece) = await AuthorizeAsync(conversationId, true, ct);
        var selected = attachments?.Distinct().ToArray() ?? [];
        var rows = await EligibleSources(conversationId, piece.CasoId).Where(d => d.ConversaId == null || d.Vinculado || selected.Contains(d.Id))
            .OrderBy(d => d.CriadoEm).Take(251).ToArrayAsync(ct);
        if (rows.Length > 250 || selected.Length > Settings.MaxAttachments) throw Limit();
        if (selected.Any(id => !rows.Any(d => d.Id == id && d.ConversaId == conversationId))) throw Missing();
        await db.AssistenteDocumentos.Where(d => d.ConversaId == conversationId && selected.Contains(d.Id)).ExecuteUpdateAsync(s => s.SetProperty(d => d.Vinculado, true), ct);
        return rows.Select(d => new AssistenteSourceSnapshot(d.Id, d.Sha256, d.StorageId)).ToArray();
    }

    public async Task<bool> SnapshotValidAsync(IReadOnlyList<AssistenteSourceSnapshot> snapshot, Guid conversationId, Guid caseId, CancellationToken ct)
    {
        var ids = snapshot.Select(s => s.Id).ToArray();
        var current = await EligibleSources(conversationId, caseId).Where(d => ids.Contains(d.Id)).ToArrayAsync(ct);
        return current.Length == snapshot.Count && snapshot.All(s => current.Any(d => d.Id == s.Id && d.StorageId == s.StorageId && (s.Sha256 == null || d.Sha256 == s.Sha256)));
    }

    public async Task<bool> ManifestValidAsync(JsonDocument? manifest, Guid conversaId, Guid casoId, CancellationToken ct, bool lockSources = false)
    {
        if (manifest is null) return true;
        if (!Settings.Enabled) return manifest.RootElement.GetArrayLength() == 0;
        var currentQuality = DocumentQuality.VersionFor(Settings);
        foreach (var item in manifest.RootElement.EnumerateArray())
        {
            var id = item.GetProperty("id").GetGuid();
            if (lockSources)
            {
                // Publication/application hold these locks only for their short
                // database transaction; extraction and provider calls hold none.
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM ai_documents WHERE id = {id} FOR SHARE", ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM documentos_caso WHERE id = (SELECT documento_caso_id FROM ai_documents WHERE id = {id}) FOR SHARE", ct);
            }
            var source = await EligibleSources(conversaId, casoId).SingleOrDefaultAsync(d => d.Id == id, ct);
            if (source is null || source.Estado is "Recebido" or "Processando" || source.Sha256 != item.GetProperty("hash").GetString() ||
                source.ExtratorVersao != item.GetProperty("extractor").GetString() || source.QualidadeVersao != item.GetProperty("quality").GetString() ||
                source.LeaseToken != (item.GetProperty("revision").ValueKind == JsonValueKind.Null ? (Guid?)null : item.GetProperty("revision").GetGuid())) return false;
            if ((!item.TryGetProperty("utilized", out var used) || used.GetBoolean()) && source.QualidadeVersao != currentQuality) return false;
        }
        return true;
    }
    private IQueryable<AssistenteDocumento> EligibleSources(Guid conversationId, Guid caseId) => db.AssistenteDocumentos.AsNoTracking().Where(d => d.OrganizationId == auth.OrganizationId && d.CasoId == caseId &&
        (d.ConversaId == conversationId || d.ConversaId == null && db.DocumentosCaso.Any(c => c.Id == d.DocumentoCasoId && c.CasoId == caseId && c.StorageId == d.StorageId)));
    private async Task EnsureCaseSourcesAsync(Guid caseId, CancellationToken ct)
    {
        var documents = await db.DocumentosCaso.AsNoTracking().Where(d => d.CasoId == caseId).Take(251).ToArrayAsync(ct);
        if (documents.Length > 250) throw new AssistenteIaException(413, "AI_DOCUMENT_SOURCE_LIMIT", "O Caso excede o limite de documentos desta etapa.");
        foreach (var d in documents)
        {
            var id = Guid.NewGuid(); var format = Path.GetExtension(d.NomeOriginal).TrimStart('.').ToLowerInvariant(); var now = DateTime.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO ai_documents (id, organization_id, caso_id, documento_caso_id, nome, formato, storage_id, tamanho_bytes,
                    estado, vinculado, paginas_concluidas, tentativas, extrator_versao, qualidade_versao, criado_em)
                VALUES ({id}, {auth.OrganizationId}, {caseId}, {d.Id}, {d.NomeOriginal}, {format}, {d.StorageId}, {d.TamanhoBytes},
                    'Recebido', true, 0, 0, 'documentos-v1', 'qualidade-v1', {now}) ON CONFLICT DO NOTHING
                """, ct);
        }
    }
    private async Task RequeueOutdatedAsync(Guid conversationId, Guid caseId, CancellationToken ct)
    {
        var version = DocumentQuality.VersionFor(Settings);
        await EligibleSources(conversationId, caseId).Where(d => d.Estado != "Recebido" && d.Estado != "Processando" &&
            d.Estado != "Falhou" && d.QualidadeVersao != version).ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Estado, "Recebido").SetProperty(d => d.Tentativas, 0)
                .SetProperty(d => d.LeaseToken, (Guid?)null).SetProperty(d => d.LeaseAte, (DateTime?)null), ct);
    }
    private async Task<(AssistenteConversa, Peca)> AuthorizeAsync(Guid id, bool write, CancellationToken ct)
    {
        if (write)
        {
            if (!Settings.Enabled) throw new AssistenteIaException(503, "AI_DOCUMENTS_DISABLED", "A leitura documental não está habilitada.");
            await features.ExigirModoAsync(auth.OrganizationId, AssistenteModo.Perguntar, ct);
            await transparency.ExigirConfirmacaoAsync(ct);
        }
        var conversation = await workspace.ObterConversaAsync(id, auth.OrganizationId, auth.UserId, false, ct) ?? throw Missing();
        var piece = await pecas.ObterPecaAsync(conversation.PecaId, auth.OrganizationId, ct) ?? throw Missing();
        return (conversation, piece);
    }
    private async Task<AssistenteDocumentoDto> DtoAsync(AssistenteDocumento source, CancellationToken ct)
    {
        var parts = await db.AssistenteDocumentoTrechos.AsNoTracking().Where(c => c.DocumentoId == source.Id).Select(c => new { c.Pagina, c.Localizador, c.Qualidade, c.Motivo }).ToArrayAsync(ct);
        return new(source.Id, source.Nome, source.ConversaId.HasValue ? "AnexoPrivado" : "DocumentoCaso", source.Formato,
            source.TamanhoBytes, source.Estado, source.PaginasTotal, source.PaginasConcluidas,
            parts.Count(p => p.Qualidade == "Confiavel"), parts.Count(p => p.Qualidade != "Confiavel"), source.CodigoErro,
            parts.Where(p => p.Qualidade != "Confiavel").Select(p => new AssistenteDocumentoProblema(p.Pagina, p.Localizador, p.Motivo ?? "UNREADABLE")).ToArray(), source.Vinculado);
    }
    public static bool ValidSignature(ReadOnlySpan<byte> bytes, string format) => format switch
    {
        "pdf" => bytes.StartsWith("%PDF-"u8), "docx" => bytes.StartsWith("PK\x03\x04"u8),
        "png" => bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "jpg" or "jpeg" => bytes.StartsWith(new byte[] { 255, 216, 255 }),
        "txt" => !bytes.Contains((byte)0), _ => false
    };
    private static AssistenteIaException Missing() => new(404, "AI_RESOURCE_NOT_FOUND", "Recurso não encontrado.");
    private static AssistenteIaException Limit() => new(413, "AI_UPLOAD_TOO_LARGE", "O envio excede o limite de arquivo ou da conversa. Nenhum anexo existente foi apagado.");
}

public sealed record AssistenteSourceSnapshot(Guid Id, string? Sha256, string StorageId);
