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
        Status = UserStatus.Pending;
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

    public void ConfirmEmail()
    {
        EmailConfirmedAt ??= DateTimeOffset.UtcNow;
        if (Status == UserStatus.Pending)
            Status = UserStatus.Active;
        Touch();
    }

    public void RegisterLogin()
    {
        LastLoginAt = DateTimeOffset.UtcNow;
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
