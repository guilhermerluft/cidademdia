using Microsoft.Extensions.Configuration;

namespace CidadeEmDia.Infrastructure.Identity;

public sealed record JwtOptions(
    string Issuer,
    string Audience,
    string SigningKey,
    TimeSpan AccessTokenLifetime,
    TimeSpan RefreshTokenLifetime)
{
    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        var issuer = configuration["JWT_ISSUER"] ?? configuration["Jwt:Issuer"] ?? "CidadeEmDia";
        var audience = configuration["JWT_AUDIENCE"] ?? configuration["Jwt:Audience"] ?? "CidadeEmDia.Web";
        var signingKey = configuration["JWT_SIGNING_KEY"] ?? configuration["Jwt:SigningKey"];

        if (string.IsNullOrWhiteSpace(signingKey) || System.Text.Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException("JWT_SIGNING_KEY must contain at least 32 bytes.");

        return new JwtOptions(
            issuer,
            audience,
            signingKey,
            TimeSpan.FromMinutes(15),
            TimeSpan.FromDays(30));
    }
}
