namespace CidadeEmDia.Application.Authentication;

public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Roles);

public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthenticatedUser User);

public sealed record AuthResult(bool Succeeded, string? ErrorCode, AuthSession? Session)
{
    public static AuthResult Success(AuthSession session) => new(true, null, session);
    public static AuthResult Failure(string errorCode) => new(false, errorCode, null);
}
