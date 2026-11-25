using System.Security.Cryptography;
using System.Text;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Infrastructure.Storage;

/// <summary>
/// Decorator transparente sobre <see cref="IFileStorage"/> que aplica criptografia
/// autenticada AES-256-GCM em repouso (formato: [JAI1][12B nonce][16B tag][ciphertext])
/// vinculando o <c>storageId</c> como AAD para impedir troca indevida de arquivos.
/// Arquivos legados sem o cabeçalho <c>JAI1</c> continuam legíveis de forma retrocompatível.
/// </summary>
public sealed class EncryptedFileStorage : IFileStorage
{
    private static readonly byte[] Magic = "JAI1"u8.ToArray();
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int HeaderSize = 4 + NonceSize + TagSize;

    private readonly IFileStorage _inner;
    private readonly byte[]? _key;

    public EncryptedFileStorage(IFileStorage inner, IOptions<DocumentStorageSettings> options)
    {
        _inner = inner;
        if (!options.Value.TryGetEncryptionKey(out var key))
            throw new InvalidOperationException("DocumentStorage:EncryptionKeyBase64 deve ser uma chave Base64 válida de 32 bytes (256 bits).");
        _key = key;
    }

    public async Task SaveAsync(string storageId, Stream content, CancellationToken cancellationToken)
    {
        if (_key is null)
        {
            await _inner.SaveAsync(storageId, content, cancellationToken);
            return;
        }

        using var plainBuffer = new MemoryStream();
        await content.CopyToAsync(plainBuffer, cancellationToken);
        var plaintext = plainBuffer.GetBuffer().AsSpan(0, (int)plainBuffer.Length);

        var output = new byte[HeaderSize + plaintext.Length];
        Magic.CopyTo(output, 0);
        var nonce = output.AsSpan(4, NonceSize);
        var tag = output.AsSpan(4 + NonceSize, TagSize);
        var ciphertext = output.AsSpan(HeaderSize);

        RandomNumberGenerator.Fill(nonce);
        var aad = Encoding.UTF8.GetBytes(storageId);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);

        using var encryptedStream = new MemoryStream(output, writable: false);
        await _inner.SaveAsync(storageId, encryptedStream, cancellationToken);
    }

    public async Task<Stream> GetAsync(string storageId, CancellationToken cancellationToken)
    {
        await using var rawStream = await _inner.GetAsync(storageId, cancellationToken);
        using var buffer = new MemoryStream();
        await rawStream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        if (bytes.Length < HeaderSize || !bytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            // Retrocompatibilidade com arquivos gravados antes da ativação da criptografia
            return new MemoryStream(bytes, writable: false);
        }

        if (_key is null)
            throw new InvalidOperationException("O arquivo está criptografado, mas DocumentStorage:EncryptionKeyBase64 não está configurada.");

        var nonce = bytes.AsSpan(4, NonceSize);
        var tag = bytes.AsSpan(4 + NonceSize, TagSize);
        var ciphertext = bytes.AsSpan(HeaderSize);
        var plaintext = new byte[ciphertext.Length];
        var aad = Encoding.UTF8.GetBytes(storageId);

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);

        return new MemoryStream(plaintext, writable: false);
    }

    public Task DeleteAsync(string storageId, CancellationToken cancellationToken) =>
        _inner.DeleteAsync(storageId, cancellationToken);
}
