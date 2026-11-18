namespace JuridicoAI.Api.Application.Abstractions;

public interface IPecaPdfConverter
{
    Task<byte[]> ConvertAsync(byte[] docx, CancellationToken ct);
}
