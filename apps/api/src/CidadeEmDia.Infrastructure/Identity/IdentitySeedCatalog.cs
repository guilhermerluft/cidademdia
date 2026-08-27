using CidadeEmDia.Domain.Identity;

namespace CidadeEmDia.Infrastructure.Identity;

public static class IdentitySeedCatalog
{
    public static IReadOnlyCollection<(string Key, string Description)> Permissions { get; } =
    [
        (IdentityPermissionKeys.ProfileReadSelf, "Ler o próprio perfil privado."),
        (IdentityPermissionKeys.ProfileUpdateSelf, "Atualizar o próprio perfil."),
        (IdentityPermissionKeys.MasterScopeAccess, "Acessar recursos vinculados ao escopo da conta Master."),
        (IdentityPermissionKeys.AdminAccess, "Acessar recursos administrativos protegidos.")
    ];

    public static IReadOnlyCollection<(string Key, string Name, IReadOnlyCollection<string> Permissions)> Roles { get; } =
    [
        (IdentityRoleKeys.Citizen, "Cidadão", [IdentityPermissionKeys.ProfileReadSelf, IdentityPermissionKeys.ProfileUpdateSelf]),
        (IdentityRoleKeys.Master, "Master", [IdentityPermissionKeys.ProfileReadSelf, IdentityPermissionKeys.ProfileUpdateSelf, IdentityPermissionKeys.MasterScopeAccess]),
        (IdentityRoleKeys.Subaccount, "Subconta", [IdentityPermissionKeys.ProfileReadSelf, IdentityPermissionKeys.ProfileUpdateSelf]),
        (IdentityRoleKeys.Admin, "Administrador", IdentityPermissionKeys.All)
    ];
}
