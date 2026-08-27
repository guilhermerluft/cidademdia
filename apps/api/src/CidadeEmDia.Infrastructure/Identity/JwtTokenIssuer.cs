using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CidadeEmDia.Domain.Identity;
using Microsoft.IdentityModel.Tokens;

namespace CidadeEmDia.Infrastructure.Identity;

internal sealed class JwtTokenIssuer(JwtOptions options)
{
    public (string Token, DateTimeOffset ExpiresAt) Issue(
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        DateTimeOffset now)
    {
        var expiresAt = now.Add(options.AccessTokenLifetime);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        claims.AddRange(roles
            .Distinct(StringComparer.Ordinal)
            .Select(role => new Claim(ClaimTypes.Role, role)));

        claims.AddRange(permissions
            .Distinct(StringComparer.Ordinal)
            .Select(permission => new Claim(IdentityClaimTypes.Permission, permission)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
