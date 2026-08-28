using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Identity;

public sealed class SubaccountInvitation : BaseEntity
{
    private SubaccountInvitation() { }

    public SubaccountInvitation(
        Guid masterUserId,
        string email,
        string tokenHash,
        string permissionKeysJson,
        DateTimeOffset expiresAt)
    {
        if (masterUserId == Guid.Empty)
            throw new ArgumentException("Master user id is required.", nameof(masterUserId));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("E-mail is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        if (string.IsNullOrWhiteSpace(permissionKeysJson))
            throw new ArgumentException("Permission snapshot is required.", nameof(permissionKeysJson));

        MasterUserId = masterUserId;
        Email = email.Trim();
        NormalizedEmail = Email.ToUpperInvariant();
        TokenHash = tokenHash;
        PermissionKeysJson = permissionKeysJson;
        ExpiresAt = expiresAt;
    }

    public Guid MasterUserId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public string PermissionKeysJson { get; private set; } = "[]";
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public User MasterUser { get; private set; } = null!;

    public bool IsActive(DateTimeOffset now) => AcceptedAt is null && RevokedAt is null && ExpiresAt > now;

    public void Accept(DateTimeOffset now)
    {
        if (!IsActive(now))
            throw new InvalidOperationException("Only active invitations can be accepted.");

        AcceptedAt = now;
        Touch();
    }

    public void Revoke(DateTimeOffset now)
    {
        if (AcceptedAt is not null || RevokedAt is not null)
            return;

        RevokedAt = now;
        Touch();
    }
}
