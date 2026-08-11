namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class DocumentStorageSettings
{
    public string RootPath { get; init; } = "data/documentos";
    public long MaxFileSizeBytes { get; init; } = 20 * 1024 * 1024;
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
}
