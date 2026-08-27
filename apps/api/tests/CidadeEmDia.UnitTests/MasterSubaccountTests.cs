using CidadeEmDia.Domain.Identity;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class MasterSubaccountTests
{
    [Fact]
    public void RevokeAndReactivate_ChangesStatusImmediately()
    {
        var masterId = Guid.NewGuid();
        var subaccountId = Guid.NewGuid();
        var link = new MasterSubaccount(masterId, subaccountId);

        Assert.True(link.IsActive);

        var revokedAt = DateTimeOffset.UtcNow;
        link.Revoke(revokedAt);

        Assert.False(link.IsActive);
        Assert.Equal(MasterSubaccountStatus.Revoked, link.Status);
        Assert.Equal(revokedAt, link.RevokedAt);

        link.Reactivate();

        Assert.True(link.IsActive);
        Assert.Equal(MasterSubaccountStatus.Active, link.Status);
        Assert.Null(link.RevokedAt);
    }

    [Fact]
    public void Constructor_RejectsSelfLink()
    {
        var userId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new MasterSubaccount(userId, userId));
    }

    [Fact]
    public void ScopedPermissionCatalog_IsUnique()
    {
        Assert.Equal(
            SubaccountPermissionKeys.All.Count,
            SubaccountPermissionKeys.All.Distinct(StringComparer.Ordinal).Count());
    }
}
