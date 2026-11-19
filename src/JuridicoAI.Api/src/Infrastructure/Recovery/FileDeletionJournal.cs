using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Infrastructure.Recovery;

/// <summary>A signed, bounded, atomically replaced registry on an independent mounted volume.</summary>
public sealed class FileDeletionJournal(IOptions<DeletionProtectionSettings> options,
    IOptions<DocumentStorageSettings> storage, IHostEnvironment environment) : IAssistenteDeletionJournal
{
    private const int MaxBytes = 32 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private DeletionProtectionSettings Settings => options.Value;
    public bool Configured => Settings.Configured;
    private string DirectoryPath
    {
        get
        {
            if (!Configured) throw Unavailable();
            var directory = Path.GetFullPath(Settings.JournalDirectory);
            var content = Path.GetFullPath(environment.ContentRootPath);
            var documents = Path.GetFullPath(Path.IsPathRooted(storage.Value.RootPath) ? storage.Value.RootPath : Path.Combine(content, storage.Value.RootPath));
            if (Within(directory, content) || Within(directory, documents) || Within(content, directory) || Within(documents, directory)) throw Unavailable();
            EnsureNoLinks(directory);
            return directory;
        }
    }
    public async Task InitializeAsync(CancellationToken ct)
    {
        var directory = DirectoryPath;
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await using var held = await LockAsync(directory, ct);
        if (File.Exists(Path.Combine(directory, "authorizations.json"))) { await ReadCoreAsync(directory, ct); SyncDirectory(directory); return; }
        await WriteAsync(directory, [], ct);
    }
    public async Task AppendAsync(AssistenteDeletionAuthorization record, CancellationToken ct)
    {
        Validate(record);
        try
        {
            var directory = DirectoryPath;
            await using var held = await LockAsync(directory, ct);
            var current = await ReadCoreAsync(directory, ct);
            var previous = current.Records.FirstOrDefault(r => r.RequestId == record.RequestId || r.ConversationId == record.ConversationId);
            if (previous is not null)
            {
                if (previous != record) throw Unavailable();
                SyncDirectory(directory);
                return;
            }
            await WriteAsync(directory, current.Records.Append(record).OrderBy(r => r.RequestId).ToArray(), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is not AssistenteIaException) { throw Unavailable(); }
    }
    public async Task<AssistenteDeletionJournalSnapshot> ReadAsync(CancellationToken ct)
    {
        try
        {
            var directory = DirectoryPath;
            await using var held = await LockAsync(directory, ct);
            return await ReadCoreAsync(directory, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is not AssistenteIaException) { throw Unavailable(); }
    }
    private async Task<AssistenteDeletionJournalSnapshot> ReadCoreAsync(string directory, CancellationToken ct)
    {
        var path = Path.Combine(directory, "authorizations.json"); EnsureNoLinks(path);
        if (!File.Exists(path) || new FileInfo(path).Length is <= 0 or > MaxBytes) throw Unavailable();
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var envelope = JsonSerializer.Deserialize<Envelope>(bytes, Json) ?? throw Unavailable();
        if (envelope.Version != 1 || envelope.ScopeId != Settings.ScopeId || envelope.Records is null || envelope.Records.Length > 100000) throw Unavailable();
        foreach (var record in envelope.Records) Validate(record);
        if (envelope.Records.Select(r => r.RequestId).Distinct().Count() != envelope.Records.Length ||
            envelope.Records.Select(r => r.ConversationId).Distinct().Count() != envelope.Records.Length) throw Unavailable();
        var expected = Sign(envelope.Records);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(envelope.Signature), expected)) throw Unavailable();
        return new(Convert.ToHexStringLower(SHA256.HashData(bytes)), Array.AsReadOnly(envelope.Records));
    }
    private byte[] Sign(AssistenteDeletionAuthorization[] records) => HMACSHA256.HashData(Convert.FromBase64String(Settings.IntegrityKeyBase64),
        JsonSerializer.SerializeToUtf8Bytes(new Payload(1, Settings.ScopeId, records), Json));
    private async Task WriteAsync(string directory, AssistenteDeletionAuthorization[] records, CancellationToken ct)
    {
        if (records.Length > 100000) throw Unavailable();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Envelope(1, Settings.ScopeId, records, Convert.ToBase64String(Sign(records))), Json);
        if (bytes.Length > MaxBytes) throw Unavailable();
        var temp = Path.Combine(directory, ".writing-" + Guid.NewGuid().ToString("N"));
        try
        {
            var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
                Share = FileShare.None, Options = FileOptions.Asynchronous | FileOptions.WriteThrough };
            if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var file = new FileStream(temp, fileOptions))
            { await file.WriteAsync(bytes, ct); await file.FlushAsync(ct); file.Flush(flushToDisk: true); }
            File.Move(temp, Path.Combine(directory, "authorizations.json"), overwrite: true);
            SyncDirectory(directory);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static async Task<FileStream> LockAsync(string directory, CancellationToken ct)
    {
        var path = Path.Combine(directory, ".journal.lock"); EnsureNoLinks(path);
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(5)) { await Task.Delay(25, ct); }
        }
    }
    public static void EnsureNoLinks(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Directory.GetParent(current)?.FullName)
        {
            try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw Unavailable(); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
    public static bool Within(string path, string root) => path.Equals(root, StringComparison.Ordinal) || path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    private static void Validate(AssistenteDeletionAuthorization record)
    {
        if (record is null || record.RequestId == Guid.Empty || record.UserId == Guid.Empty || record.ConversationId == Guid.Empty ||
            record.AuthorizedAt.Kind != DateTimeKind.Utc || record.AuthorizedAt < DateTime.UnixEpoch || record.AuthorizedAt.Ticks % 10 != 0) throw Unavailable();
    }
    private static void SyncDirectory(string directory)
    {
        if (!OperatingSystem.IsLinux()) return; // Files are flushed on every platform; directory fsync is available on Linux.
        var fd = open(directory, 0x10000); // O_DIRECTORY
        if (fd < 0) throw Unavailable();
        try { if (fsync(fd) != 0) throw Unavailable(); } finally { close(fd); }
    }
    [DllImport("libc", SetLastError = true)] private static extern int open(string path, int flags);
    [DllImport("libc", SetLastError = true)] private static extern int fsync(int fd);
    [DllImport("libc", SetLastError = true)] private static extern int close(int fd);
    public static AssistenteIaException Unavailable() => new(503, "AI_DELETION_JOURNAL_UNAVAILABLE", "Não foi possível confirmar o registro protegido da exclusão. A conversa permanece bloqueada quando a autorização já foi gravada; tente novamente.", retryable: true);
    private sealed record Payload(int Version, Guid ScopeId, AssistenteDeletionAuthorization[] Records);
    private sealed record Envelope(int Version, Guid ScopeId, AssistenteDeletionAuthorization[] Records, string Signature);
}
