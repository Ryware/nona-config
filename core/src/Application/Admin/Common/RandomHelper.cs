using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;

namespace Nona.Application.Admin.Common
{
    internal static class TokenHelper
    {
        internal static string Generate()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return WebEncoders.Base64UrlEncode(bytes);
        }

        internal static string Hash(string token)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(token);
            return Convert.ToHexString(sha.ComputeHash(bytes));
        }
    }
}
