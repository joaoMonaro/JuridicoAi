using System.Security.Cryptography;
using System.Text;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Infrastructure.Settings;
using JuridicoAI.Api.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed class EncryptedFileStorageTests
{
    private static string NewKeyBase64()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }

    [Fact]
    public async Task SaveEGet_CriptografaEmRepousoERestauraConteudoTransparente()
    {
        var inner = new MemoryStorage();
        var storage = new EncryptedFileStorage(inner, Options.Create(new DocumentStorageSettings
        {
            EncryptionKeyBase64 = NewKeyBase64()
        }));

        const string original = "Contrato sigiloso com CPF 529.982.247-25 e dados médicos.";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(original));
        await storage.SaveAsync("casos/1/doc.txt", input, default);

        var raw = inner.Files["casos/1/doc.txt"];
        Assert.Equal("JAI1"u8.ToArray(), raw[..4]);
        Assert.DoesNotContain("529.982.247-25", Encoding.UTF8.GetString(raw));

        await using var output = await storage.GetAsync("casos/1/doc.txt", default);
        using var reader = new StreamReader(output, Encoding.UTF8);
        Assert.Equal(original, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Get_DetectaAdulteracaoETrocaDeStorageId()
    {
        var inner = new MemoryStorage();
        var storage = new EncryptedFileStorage(inner, Options.Create(new DocumentStorageSettings
        {
            EncryptionKeyBase64 = NewKeyBase64()
        }));

        using var input = new MemoryStream(Encoding.UTF8.GetBytes("Conteúdo confidencial."));
        await storage.SaveAsync("casos/1/a.txt", input, default);

        // Troca de arquivo entre caminhos diferentes falha na validação AAD
        inner.Files["casos/2/b.txt"] = inner.Files["casos/1/a.txt"].ToArray();
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => storage.GetAsync("casos/2/b.txt", default));

        // Adulteração de 1 byte no ciphertext falha na validação GCM
        var tampered = inner.Files["casos/1/a.txt"].ToArray();
        tampered[^1] ^= 0xFF;
        inner.Files["casos/1/a.txt"] = tampered;
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => storage.GetAsync("casos/1/a.txt", default));
    }

    [Fact]
    public async Task Get_LeArquivoLegadoSemCabecalhoERecusaCriptografadoSemChave()
    {
        var inner = new MemoryStorage();
        inner.Files["legado/antigo.txt"] = Encoding.UTF8.GetBytes("Arquivo anterior à criptografia.");

        var encrypted = new EncryptedFileStorage(inner, Options.Create(new DocumentStorageSettings
        {
            EncryptionKeyBase64 = NewKeyBase64()
        }));
        await using (var legacy = await encrypted.GetAsync("legado/antigo.txt", default))
        using (var reader = new StreamReader(legacy, Encoding.UTF8))
            Assert.Equal("Arquivo anterior à criptografia.", await reader.ReadToEndAsync());

        using var fresh = new MemoryStream(Encoding.UTF8.GetBytes("Novo arquivo criptografado."));
        await encrypted.SaveAsync("novo/cifrado.txt", fresh, default);

        var unconfigured = new EncryptedFileStorage(inner, Options.Create(new DocumentStorageSettings()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => unconfigured.GetAsync("novo/cifrado.txt", default));
    }

    [Fact]
    public void Settings_ValidaTamanhoEFormatoDaChaveBase64()
    {
        Assert.True(new DocumentStorageSettings { EncryptionKeyBase64 = null }.TryGetEncryptionKey(out var nullKey));
        Assert.Null(nullKey);

        Assert.True(new DocumentStorageSettings { EncryptionKeyBase64 = NewKeyBase64() }.TryGetEncryptionKey(out var validKey));
        Assert.Equal(32, validKey!.Length);

        Assert.False(new DocumentStorageSettings { EncryptionKeyBase64 = "nao-e-base64!!!" }.TryGetEncryptionKey(out _));
        Assert.False(new DocumentStorageSettings { EncryptionKeyBase64 = Convert.ToBase64String(new byte[16]) }.TryGetEncryptionKey(out _));
    }

    private sealed class MemoryStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

        public async Task SaveAsync(string storageId, Stream content, CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, cancellationToken);
            Files[storageId] = ms.ToArray();
        }

        public Task<Stream> GetAsync(string storageId, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream(Files[storageId], writable: false));

        public Task DeleteAsync(string storageId, CancellationToken cancellationToken)
        {
            Files.Remove(storageId);
            return Task.CompletedTask;
        }
    }
}
