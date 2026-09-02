using System.Security.Cryptography;
using System.Text;

namespace Nona.Application.Common;

public static class ApiKeySecret
{
    public const int CurrentHashVersion = 1;
    private const int FingerprintLength = 8;

    public static string Generate()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }

    public static string Fingerprint(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return secret[^Math.Min(FingerprintLength, secret.Length)..];
    }
}
