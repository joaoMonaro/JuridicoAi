namespace JuridicoAI.Api.Application.Abstractions;

public interface IFileStorage
{
    Task SaveAsync(string storageId, Stream content, CancellationToken cancellationToken);
    Task<Stream> GetAsync(string storageId, CancellationToken cancellationToken);
    Task DeleteAsync(string storageId, CancellationToken cancellationToken);
}
