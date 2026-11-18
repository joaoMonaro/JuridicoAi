using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Infrastructure.Export;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed class LibreOfficePdfConverterTests
{
    [Fact]
    public async Task ConversorAusenteRetorna503ELimpaDiretorio()
    {
        var root = Path.Combine(Path.GetTempPath(), "export-test-" + Guid.NewGuid());
        try
        {
            using var converter = Create(root, Path.Combine(root, "inexistente"));
            var error = await Assert.ThrowsAsync<ExportacaoException>(() => converter.ConvertAsync([1, 2, 3], default));
            Assert.Equal(503, error.StatusCode); Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("exit 1", 503)]
    [InlineData("exec sleep 30", 504)]
    [InlineData("for last do :; done\nprintf '%s' '%PDF-1.7 fake' > \"$(dirname \"$last\")/peca.pdf\"", 200)]
    public async Task TrataFalhaTimeoutESucessoSemTemporariosAbandonados(string script, int expected)
    {
        if (OperatingSystem.IsWindows()) return; // Executável de teste POSIX, não é requisito do conversor.
        var root = Path.Combine(Path.GetTempPath(), "export-test-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        var executable = Path.Combine(root, "converter"); await File.WriteAllTextAsync(executable, "#!/bin/sh\n" + script + "\n");
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var work = Path.Combine(root, "work");
        try
        {
            using var converter = Create(work, executable);
            if (expected == 200) Assert.True((await converter.ConvertAsync([1], default)).AsSpan().StartsWith("%PDF-"u8));
            else { var error = await Assert.ThrowsAsync<ExportacaoException>(() => converter.ConvertAsync([1], default)); Assert.Equal(expected, error.StatusCode); }
            Assert.Empty(Directory.EnumerateFileSystemEntries(work));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task LimitaConcorrenciaECancelamentoLiberaVaga()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "export-test-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        var executable = Path.Combine(root, "converter"); await File.WriteAllTextAsync(executable, "#!/bin/sh\nexec sleep 30\n");
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var work = Path.Combine(root, "work");
        try
        {
            using var converter = Create(work, executable, 10); using var cancellation = new CancellationTokenSource();
            var first = converter.ConvertAsync([1], cancellation.Token);
            var busy = await Assert.ThrowsAsync<ExportacaoException>(() => converter.ConvertAsync([2], default)); Assert.Equal(503, busy.StatusCode);
            cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            Assert.Empty(Directory.EnumerateFileSystemEntries(work));
            using var alreadyCanceled = new CancellationTokenSource(); alreadyCanceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => converter.ConvertAsync([3], alreadyCanceled.Token));
        }
        finally { Directory.Delete(root, true); }
    }

    private static LibreOfficePdfConverter Create(string root, string executable, int timeout = 1) =>
        new(Options.Create(new PecaExportSettings { TempPath = root, LibreOfficePath = executable, TimeoutSeconds = timeout, MaxConcurrentConversions = 1 }), NullLogger<LibreOfficePdfConverter>.Instance);
}
