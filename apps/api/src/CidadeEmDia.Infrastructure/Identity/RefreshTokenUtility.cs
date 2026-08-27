using System.Security.Cryptography;
using System.Text;

namespace CidadeEmDia.Infrastructure.Identity;

internal static class RefreshTokenUtility
{
    public static string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
