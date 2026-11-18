namespace JuridicoAI.Api.Infrastructure.Documents;
// Private scratch belongs to the same deletion scope as the original attachment.
public static class AssistenteDocumentScratch
{
    public static string Root => Path.Combine(Path.GetTempPath(), "juridicoai-extract");
    public static string Private(Guid conversationId) => Path.Combine(Root, "private-" + conversationId.ToString("N"));
    public static string Create(Guid? conversationId)
    {
        var path = Path.Combine(conversationId.HasValue ? Private(conversationId.Value) : Path.Combine(Root, "case"), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
    public static void DeletePrivate(Guid conversationId)
    { var path = Private(conversationId); if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
}
