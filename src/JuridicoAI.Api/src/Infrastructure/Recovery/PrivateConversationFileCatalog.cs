using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Infrastructure.Recovery;

public sealed class PrivateConversationFileCatalog(IOptions<DocumentStorageSettings> options, IHostEnvironment environment) : IPrivateConversationFiles
{
    public Task<IReadOnlyList<string>> ListAsync(Guid conversationId, CancellationToken ct)
    {
        if (conversationId == Guid.Empty) throw new InvalidOperationException("Escopo privado inválido.");
        var root = Path.GetFullPath(Path.IsPathRooted(options.Value.RootPath) ? options.Value.RootPath : Path.Combine(environment.ContentRootPath, options.Value.RootPath));
        var assistant = Path.Combine(root, "assistant");
        FileDeletionJournal.EnsureNoLinks(assistant);
        var files = new List<string>();
        if (!Directory.Exists(assistant)) return Task.FromResult<IReadOnlyList<string>>(files);
        foreach (var organization in Directory.EnumerateDirectories(assistant))
        {
            ct.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileName(organization), "N", out _)) throw new InvalidOperationException("Namespace privado inválido.");
            var path = Path.Combine(organization, conversationId.ToString("N"));
            FileDeletionJournal.EnsureNoLinks(path);
            if (File.Exists(path)) throw new InvalidOperationException("Namespace privado inválido.");
            if (!Directory.Exists(path)) continue;
            var visited = 0;
            var pending = new Stack<string>(); pending.Push(path);
            while (pending.TryPop(out var directory))
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                ct.ThrowIfCancellationRequested(); FileDeletionJournal.EnsureNoLinks(entry);
                if (++visited > 20000) throw new InvalidOperationException("Namespace privado excede o limite operacional.");
                if (Directory.Exists(entry)) pending.Push(entry);
                else files.Add(Path.GetRelativePath(root, entry).Replace(Path.DirectorySeparatorChar, '/'));
                if (files.Count > 10000) throw new InvalidOperationException("Escopo privado excede o limite de recuperação.");
            }
        }
        return Task.FromResult<IReadOnlyList<string>>(files.Order().ToArray());
    }
    public static bool InScope(Guid conversationId, string path) => path.Split('/') is ["assistant", var org, var conversation, ..] &&
        Guid.TryParseExact(org, "N", out _) && conversation == conversationId.ToString("N") && !path.Contains("..", StringComparison.Ordinal) && !path.Contains('\\');
}
