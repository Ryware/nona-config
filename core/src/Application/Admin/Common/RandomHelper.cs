using System.Security.Cryptography;
using System.Text;

namespace Nona.Application.Admin.Common
{
    internal static class TokenHelper
    {
        private const string UrlSafeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        internal static string Generate()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        internal static string GenerateUrlSafe(int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Token length must be greater than zero.");

            Span<char> chars = stackalloc char[length];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = UrlSafeAlphabet[RandomNumberGenerator.GetInt32(UrlSafeAlphabet.Length)];
            }

            return new string(chars);
        }

        internal static string Hash(string token)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(token);
            return Convert.ToHexString(sha.ComputeHash(bytes));
        }
    }
}
