using System.ComponentModel;
using System.Diagnostics;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Infrastructure.Export;

// Singleton: o limite é compartilhado entre todas as requisições do processo.
public sealed class LibreOfficePdfConverter(IOptions<PecaExportSettings> options, ILogger<LibreOfficePdfConverter> logger) : IPecaPdfConverter, IDisposable
{
    private readonly PecaExportSettings settings = options.Value;
    private readonly SemaphoreSlim slots = new(Math.Clamp(options.Value.MaxConcurrentConversions, 1, 8));

    public async Task<byte[]> ConvertAsync(byte[] docx, CancellationToken ct)
    {
        if (!await slots.WaitAsync(0, ct)) throw new ExportacaoException("Há outras exportações de PDF em andamento. Tente novamente em instantes.");
        string? directory = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 300)));
        using var process = new Process();
        try
        {
            directory = Path.Combine(Path.GetFullPath(settings.TempPath), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var input = Path.Combine(directory, "peca.docx");
            await File.WriteAllBytesAsync(input, docx, timeout.Token);
            process.StartInfo = new ProcessStartInfo(settings.LibreOfficePath)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = directory,
            };
            foreach (var argument in new[] { "-env:UserInstallation=" + new Uri(Path.Combine(directory, "profile")).AbsoluteUri,
                "--headless", "--nologo", "--nodefault", "--nofirststartwizard", "--convert-to", "pdf:writer_pdf_Export", "--outdir", directory, input })
                process.StartInfo.ArgumentList.Add(argument);
            process.StartInfo.Environment["SAL_USE_VCLPLUGIN"] = "gen";
            process.Start();
            // Drenar os pipes sem guardar conteúdo ou mensagens do conversor nos logs.
            var stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
            var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(stdout, stderr).WaitAsync(timeout.Token);
            var output = Path.Combine(directory, "peca.pdf");
            if (process.ExitCode != 0 || !File.Exists(output)) throw new ExportacaoException("Não foi possível converter a peça para PDF. Tente novamente.");
            var bytes = await File.ReadAllBytesAsync(output, timeout.Token);
            if (!bytes.AsSpan().StartsWith("%PDF-"u8)) throw new ExportacaoException("O conversor não produziu um PDF válido.");
            return bytes;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new ExportacaoException("A geração do PDF excedeu o tempo permitido. Tente novamente.", timeout: true); }
        catch (Win32Exception)
        { throw new ExportacaoException("O conversor de PDF está indisponível. A exportação Word continua disponível."); }
        catch (IOException)
        { throw new ExportacaoException("Não foi possível preparar os arquivos da exportação. Tente novamente."); }
        catch (UnauthorizedAccessException)
        { throw new ExportacaoException("O diretório temporário da exportação está indisponível."); }
        catch (ArgumentException)
        { throw new ExportacaoException("A configuração do conversor de PDF é inválida."); }
        finally
        {
            try { if (process.Id > 0 && !process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { logger.LogWarning("Não foi possível encerrar o conversor de PDF."); }
            try { if (directory is not null && Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
            catch (IOException) { logger.LogWarning("Não foi possível remover temporários de uma exportação."); }
            catch (UnauthorizedAccessException) { logger.LogWarning("Sem permissão para remover temporários de uma exportação."); }
            slots.Release();
        }
    }

    public void Dispose() => slots.Dispose();
}
