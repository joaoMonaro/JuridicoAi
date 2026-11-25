namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class DocumentStorageSettings
{
    public string RootPath { get; init; } = "data/documentos";
    public long MaxFileSizeBytes { get; init; } = 20 * 1024 * 1024;
    public string? EncryptionKeyBase64 { get; init; }
    public string[] AllowedExtensions { get; init; } =
        [".pdf", ".doc", ".docx", ".txt", ".png", ".jpg", ".jpeg"];
    public string[] AllowedMimeTypes { get; init; } =
    [
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain",
        "image/png",
        "image/jpeg"
    ];

    public bool TryGetEncryptionKey(out byte[]? key)
    {
        if (string.IsNullOrWhiteSpace(EncryptionKeyBase64))
        {
            key = null;
            return true;
        }

        try
        {
            var decoded = Convert.FromBase64String(EncryptionKeyBase64.Trim());
            if (decoded.Length != 32)
            {
                key = null;
                return false;
            }
            key = decoded;
            return true;
        }
        catch (FormatException)
        {
            key = null;
            return false;
        }
    }
}
