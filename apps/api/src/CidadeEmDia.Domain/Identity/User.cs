using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Identity;

public sealed class User : BaseEntity
{
    private User() { }

    public User(string email, string passwordHash)
    {
        Email = email.Trim().ToLowerInvariant();
        NormalizedEmail = Email.ToUpperInvariant();
        PasswordHash = passwordHash;
        Status = UserStatus.Active;
    }

    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserStatus Status { get; private set; }
    public DateTimeOffset? EmailConfirmedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    public UserProfile? Profile { get; private set; }
    public ICollection<UserRole> Roles { get; private set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    public bool CanAuthenticate => Status is UserStatus.Active or UserStatus.Pending;

    public void ConfirmEmail(DateTimeOffset now)
    {
        EmailConfirmedAt ??= now;
        if (Status == UserStatus.Pending)
            Status = UserStatus.Active;
        Touch();
    }

    public void RegisterLogin(DateTimeOffset now)
    {
        LastLoginAt = now;
        Touch();
    }

    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        Touch();
    }

    public void Suspend()
    {
        Status = UserStatus.Suspended;
        Touch();
    }

    public void Block()
    {
        Status = UserStatus.Blocked;
        Touch();
    }

    public void Activate()
    {
        Status = UserStatus.Active;
        Touch();
    }
}
