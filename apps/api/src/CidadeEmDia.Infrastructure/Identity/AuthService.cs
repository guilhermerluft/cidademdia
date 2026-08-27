using System.Net.Mail;
using CidadeEmDia.Application.Authentication;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CidadeEmDia.Infrastructure.Identity;

internal sealed class AuthService(
    AppDbContext dbContext,
    IPasswordHasher passwordHasher,
    JwtTokenIssuer jwtTokenIssuer,
    JwtOptions jwtOptions,
    IPasswordResetEmailSender passwordResetEmailSender,
    PasswordResetOptions passwordResetOptions,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken = default)
    {
        email = email?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;

        if (!IsValidEmail(email) || email.Length > 320 || password is null || password.Length < 8 || password.Length > 128 || displayName.Length is < 2 or > 160)
            return AuthResult.Failure("invalid_input");

        var normalizedEmail = email.ToUpperInvariant();
        if (await dbContext.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken))
            return AuthResult.Failure("email_already_registered");

        var role = await dbContext.Roles.FirstOrDefaultAsync(x => x.Key == IdentityRoleKeys.Citizen, cancellationToken);
        if (role is null)
        {
            role = new Role(IdentityRoleKeys.Citizen, "Cidadão");
            dbContext.Roles.Add(role);
        }

        var user = new User(email, passwordHasher.Hash(password));
        var profile = new UserProfile(user.Id, displayName);
        var userRole = new UserRole(user.Id, role.Id);

        dbContext.Users.Add(user);
        dbContext.UserProfiles.Add(profile);
        dbContext.UserRoles.Add(userRole);

        var session = CreateSession(user, profile, [role.Key], DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(session);
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToUpperInvariant();
        var user = await LoadUserByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !passwordHasher.Verify(password ?? string.Empty, user.PasswordHash))
            return AuthResult.Failure("invalid_credentials");

        if (!user.CanAuthenticate)
            return AuthResult.Failure("account_unavailable");

        var now = DateTimeOffset.UtcNow;
        user.RegisterLogin(now);
        var roles = user.Roles.Select(x => x.Role.Key).Distinct(StringComparer.Ordinal).ToArray();
        var session = CreateSession(user, user.Profile, roles, now);
        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(session);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return AuthResult.Failure("invalid_refresh_token");

        var now = DateTimeOffset.UtcNow;
        var tokenHash = RefreshTokenUtility.Hash(refreshToken);
        var token = await dbContext.RefreshTokens
            .Include(x => x.User)
                .ThenInclude(x => x.Profile)
            .Include(x => x.User)
                .ThenInclude(x => x.Roles)
                    .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (token is null)
            return AuthResult.Failure("invalid_refresh_token");

        if (!token.IsActive(now))
        {
            if (token.RevokedAt is not null && token.ReplacedByTokenHash is not null)
                await RevokeActiveSessionsAsync(token.UserId, now, "refresh_token_reuse_detected", cancellationToken);

            return AuthResult.Failure("invalid_refresh_token");
        }

        if (!token.User.CanAuthenticate)
        {
            token.Revoke(now, "account_unavailable");
            await dbContext.SaveChangesAsync(cancellationToken);
            return AuthResult.Failure("account_unavailable");
        }

        var newRawToken = RefreshTokenUtility.Generate();
        var newHash = RefreshTokenUtility.Hash(newRawToken);
        var newExpiresAt = now.Add(jwtOptions.RefreshTokenLifetime);
        token.Revoke(now, "rotated", newHash);
        dbContext.RefreshTokens.Add(new RefreshToken(token.UserId, newHash, newExpiresAt));

        var roles = token.User.Roles.Select(x => x.Role.Key).Distinct(StringComparer.Ordinal).ToArray();
        var (accessToken, accessExpiresAt) = jwtTokenIssuer.Issue(token.User, roles, now);
        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(new AuthSession(
            accessToken,
            accessExpiresAt,
            newRawToken,
            newExpiresAt,
            ToAuthenticatedUser(token.User, token.User.Profile, roles)));
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var hash = RefreshTokenUtility.Hash(refreshToken);
        var token = await dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (token is null || token.RevokedAt is not null)
            return;

        token.Revoke(DateTimeOffset.UtcNow, "logout");
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthenticatedUser?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.Profile)
            .Include(x => x.Roles)
                .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null || !user.CanAuthenticate)
            return null;

        var roles = user.Roles.Select(x => x.Role.Key).Distinct(StringComparer.Ordinal).ToArray();
        return ToAuthenticatedUser(user, user.Profile, roles);
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        email = email?.Trim() ?? string.Empty;
        if (!IsValidEmail(email) || email.Length > 320)
            return;

        var normalizedEmail = email.ToUpperInvariant();
        var user = await dbContext.Users
            .Include(x => x.Profile)
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || !user.CanAuthenticate)
            return;

        var now = DateTimeOffset.UtcNow;
        var previousTokens = await dbContext.PasswordResetTokens
            .Where(x => x.UserId == user.Id && x.ConsumedAt == null && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var previousToken in previousTokens)
            previousToken.Consume(now);

        var rawToken = PasswordResetTokenUtility.Generate();
        var resetToken = new PasswordResetToken(
            user.Id,
            PasswordResetTokenUtility.Hash(rawToken),
            now.Add(passwordResetOptions.TokenLifetime));

        dbContext.PasswordResetTokens.Add(resetToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await passwordResetEmailSender.SendPasswordResetAsync(
                user.Email,
                user.Profile?.DisplayName ?? user.Email,
                rawToken,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            resetToken.Consume(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            logger.LogError(exception, "Password reset e-mail delivery failed for user {UserId}.", user.Id);
        }
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        token = token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512 || newPassword is null || newPassword.Length < 8 || newPassword.Length > 128)
            return PasswordResetResult.Failure("invalid_input");

        var now = DateTimeOffset.UtcNow;
        var tokenHash = PasswordResetTokenUtility.Hash(token);
        var resetToken = await dbContext.PasswordResetTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (resetToken is null || !resetToken.IsActive(now))
            return PasswordResetResult.Failure("invalid_or_expired_reset_token");

        if (!resetToken.User.CanAuthenticate)
            return PasswordResetResult.Failure("account_unavailable");

        resetToken.User.ChangePasswordHash(passwordHasher.Hash(newPassword));
        resetToken.Consume(now);

        var otherActiveResetTokens = await dbContext.PasswordResetTokens
            .Where(x => x.UserId == resetToken.UserId && x.Id != resetToken.Id && x.ConsumedAt == null && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var otherResetToken in otherActiveResetTokens)
            otherResetToken.Consume(now);

        await RevokeActiveSessionsAsync(resetToken.UserId, now, "password_reset", cancellationToken);
        return PasswordResetResult.Success();
    }

    private AuthSession CreateSession(User user, UserProfile? profile, IReadOnlyCollection<string> roles, DateTimeOffset now)
    {
        var rawRefreshToken = RefreshTokenUtility.Generate();
        var refreshHash = RefreshTokenUtility.Hash(rawRefreshToken);
        var refreshExpiresAt = now.Add(jwtOptions.RefreshTokenLifetime);
        dbContext.RefreshTokens.Add(new RefreshToken(user.Id, refreshHash, refreshExpiresAt));

        var (accessToken, accessExpiresAt) = jwtTokenIssuer.Issue(user, roles, now);
        return new AuthSession(
            accessToken,
            accessExpiresAt,
            rawRefreshToken,
            refreshExpiresAt,
            ToAuthenticatedUser(user, profile, roles));
    }

    private async Task<User?> LoadUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        await dbContext.Users
            .Include(x => x.Profile)
            .Include(x => x.Roles)
                .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

    private async Task RevokeActiveSessionsAsync(Guid userId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var activeToken in activeTokens)
            activeToken.Revoke(now, reason);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuthenticatedUser ToAuthenticatedUser(User user, UserProfile? profile, IReadOnlyCollection<string> roles) =>
        new(user.Id, user.Email, profile?.DisplayName ?? user.Email, roles);

    private static bool IsValidEmail(string email)
    {
        try
        {
            return new MailAddress(email).Address.Equals(email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
