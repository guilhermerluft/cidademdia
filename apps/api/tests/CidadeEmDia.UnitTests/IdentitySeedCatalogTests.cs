using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Infrastructure.Identity;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class IdentitySeedCatalogTests
{
    [Fact]
    public void Admin_ReceivesAllGlobalPermissions()
    {
        var admin = IdentitySeedCatalog.Roles.Single(x => x.Key == IdentityRoleKeys.Admin);

        Assert.Equal(
            IdentityPermissionKeys.All.OrderBy(x => x, StringComparer.Ordinal),
            admin.Permissions.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void Citizen_DoesNotReceiveAdministrativePermission()
    {
        var citizen = IdentitySeedCatalog.Roles.Single(x => x.Key == IdentityRoleKeys.Citizen);

        Assert.Contains(IdentityPermissionKeys.ProfileReadSelf, citizen.Permissions);
        Assert.Contains(IdentityPermissionKeys.ProfileUpdateSelf, citizen.Permissions);
        Assert.DoesNotContain(IdentityPermissionKeys.AdminAccess, citizen.Permissions);
    }

    [Fact]
    public void MasterScopePermission_IsNotGlobalForSubaccounts()
    {
        var master = IdentitySeedCatalog.Roles.Single(x => x.Key == IdentityRoleKeys.Master);
        var subaccount = IdentitySeedCatalog.Roles.Single(x => x.Key == IdentityRoleKeys.Subaccount);

        Assert.Contains(IdentityPermissionKeys.MasterScopeAccess, master.Permissions);
        Assert.DoesNotContain(IdentityPermissionKeys.MasterScopeAccess, subaccount.Permissions);
    }
}
