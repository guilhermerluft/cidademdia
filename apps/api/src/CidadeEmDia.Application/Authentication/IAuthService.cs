namespace CidadeEmDia.Application.Authentication;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken = default);
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default);
    Task<AuthenticatedUser?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);
    Task<PasswordResetResult> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default);
}
