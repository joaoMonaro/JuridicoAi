using System.Security.Cryptography;
using System.Text;
using JuridicoAI.Api.Application.Abstractions;
using Konscious.Security.Cryptography;

namespace JuridicoAI.Api.Infrastructure.Auth;

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 3;
    private const int MemoryKb = 65536;
    private const int Parallelism = 2;

    public async Task<string> HashAsync(string password, CancellationToken ct = default)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = await DeriveAsync(password, salt, Iterations, MemoryKb, Parallelism, ct);
        return $"$argon2id$v=19$m={MemoryKb},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public async Task<bool> VerifyAsync(string password, string encodedHash, CancellationToken ct = default)
    {
        try
        {
            var parts = encodedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5 || parts[0] != "argon2id" || parts[1] != "v=19") return false;
            var parameters = parts[2].Split(',').ToDictionary(x => x[..x.IndexOf('=')], x => int.Parse(x[(x.IndexOf('=') + 1)..]));
            var salt = Convert.FromBase64String(parts[3]);
            var expected = Convert.FromBase64String(parts[4]);
            var actual = await DeriveAsync(password, salt, parameters["t"], parameters["m"], parameters["p"], ct);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (Exception ex) when (ex is FormatException or KeyNotFoundException or ArgumentException)
        { return false; }
    }

    private static async Task<byte[]> DeriveAsync(string password, byte[] salt, int iterations, int memory, int parallelism, CancellationToken ct)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        { Salt = salt, Iterations = iterations, MemorySize = memory, DegreeOfParallelism = parallelism };
        ct.ThrowIfCancellationRequested();
        return await argon.GetBytesAsync(HashSize);
    }
}
