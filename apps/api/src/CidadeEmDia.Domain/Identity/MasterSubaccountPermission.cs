namespace CidadeEmDia.Domain.Identity;

public sealed class MasterSubaccountPermission
{
    private MasterSubaccountPermission() { }

    public MasterSubaccountPermission(Guid masterSubaccountId, Guid permissionId)
    {
        if (masterSubaccountId == Guid.Empty)
            throw new ArgumentException("Master subaccount id is required.", nameof(masterSubaccountId));
        if (permissionId == Guid.Empty)
            throw new ArgumentException("Permission id is required.", nameof(permissionId));

        MasterSubaccountId = masterSubaccountId;
        PermissionId = permissionId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid MasterSubaccountId { get; private set; }
    public Guid PermissionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public MasterSubaccount MasterSubaccount { get; private set; } = null!;
    public Permission Permission { get; private set; } = null!;
}
