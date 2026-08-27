using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Identity;

public enum MasterSubaccountStatus
{
    Active = 1,
    Revoked = 2
}

public sealed class MasterSubaccount : BaseEntity
{
    private MasterSubaccount() { }

    public MasterSubaccount(Guid masterUserId, Guid subaccountUserId)
    {
        if (masterUserId == Guid.Empty)
            throw new ArgumentException("Master user id is required.", nameof(masterUserId));
        if (subaccountUserId == Guid.Empty)
            throw new ArgumentException("Subaccount user id is required.", nameof(subaccountUserId));
        if (masterUserId == subaccountUserId)
            throw new ArgumentException("Master and subaccount must be different users.");

        MasterUserId = masterUserId;
        SubaccountUserId = subaccountUserId;
        Status = MasterSubaccountStatus.Active;
    }

    public Guid MasterUserId { get; private set; }
    public Guid SubaccountUserId { get; private set; }
    public MasterSubaccountStatus Status { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public User MasterUser { get; private set; } = null!;
    public User SubaccountUser { get; private set; } = null!;
    public ICollection<MasterSubaccountPermission> Permissions { get; private set; } = new List<MasterSubaccountPermission>();

    public bool IsActive => Status == MasterSubaccountStatus.Active;

    public void Revoke(DateTimeOffset now)
    {
        if (!IsActive)
            return;

        Status = MasterSubaccountStatus.Revoked;
        RevokedAt = now;
        Touch();
    }

    public void Reactivate()
    {
        if (IsActive)
            return;

        Status = MasterSubaccountStatus.Active;
        RevokedAt = null;
        Touch();
    }
}
