using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Identity;

public sealed class RefreshToken : BaseEntity
{
    private RefreshToken() { }

    public RefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string? RevocationReason { get; private set; }

    public User User { get; private set; } = null!;

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(string reason, string? replacedByTokenHash = null)
    {
        if (RevokedAt is not null)
            return;

        RevokedAt = DateTimeOffset.UtcNow;
        RevocationReason = reason;
        ReplacedByTokenHash = replacedByTokenHash;
        Touch();
    }
}
