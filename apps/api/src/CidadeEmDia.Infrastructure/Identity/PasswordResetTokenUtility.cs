using System.Security.Cryptography;
using System.Text;

namespace CidadeEmDia.Infrastructure.Identity;

internal static class PasswordResetTokenUtility
{
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
