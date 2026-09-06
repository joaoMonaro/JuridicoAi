namespace JuridicoAI.Api.Domain.Entities;

public sealed class DocumentoCaso
{
    public Guid Id { get; init; }
    public Guid CasoId { get; init; }
    public string NomeOriginal { get; init; } = string.Empty;
    public string NomeArmazenado { get; init; } = string.Empty;
    public string Tipo { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public long TamanhoBytes { get; init; }
    public string StorageId { get; init; } = string.Empty;
    public DateTime DataUpload { get; init; }
}
