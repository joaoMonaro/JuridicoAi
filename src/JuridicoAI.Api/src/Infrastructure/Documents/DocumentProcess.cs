using System.Diagnostics;
using System.Text;
using JuridicoAI.Api.Application.Exceptions;

namespace JuridicoAI.Api.Infrastructure.Documents;

public sealed class DocumentProcess
{
    public async Task<string> RunAsync(string executable, IEnumerable<string> args, string directory,
        int seconds, CancellationToken ct, int maxCharacters = 8_000_000)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        var start = new ProcessStartInfo(executable) { WorkingDirectory = directory,
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.Environment["LC_ALL"] = "C";
        start.Environment["OMP_THREAD_LIMIT"] = "1";
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
            using var cancel = timeout.Token.Register(() => { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
            var output = ReadAsync(process.StandardOutput, maxCharacters, timeout.Token);
            var errors = ReadAsync(process.StandardError, 64_000, timeout.Token);
            try { await Task.WhenAll(process.WaitForExitAsync(timeout.Token), output, errors); }
            catch { try { process.Kill(true); } catch (InvalidOperationException) { } throw; }
            if (process.ExitCode != 0) throw Error("AI_DOCUMENT_PARSE_FAILED");
            return await output;
        }
        catch (System.ComponentModel.Win32Exception) { throw Error("AI_DOCUMENT_TOOL_UNAVAILABLE"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Error("AI_DOCUMENT_PROCESS_TIMEOUT"); }
    }
    private static async Task<string> ReadAsync(StreamReader reader, int limit, CancellationToken ct)
    {
        var result = new StringBuilder(); var buffer = new char[8192]; int count;
        while ((count = await reader.ReadAsync(buffer, ct)) > 0)
        {
            if (result.Length + count > limit) throw Error("AI_DOCUMENT_TEXT_LIMIT");
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }
    public static AssistenteIaException Error(string code) => new(422, code, "O documento não pôde ser lido integralmente. Consulte os problemas de leitura.");
}
