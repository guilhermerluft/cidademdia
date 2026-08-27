namespace CidadeEmDia.Domain.Identity;

public static class IdentityPermissionKeys
{
    public const string ProfileReadSelf = "profile.read.self";
    public const string ProfileUpdateSelf = "profile.update.self";
    public const string MasterScopeAccess = "master.scope.access";
    public const string AdminAccess = "admin.access";

    public static IReadOnlyCollection<string> All { get; } =
    [
        ProfileReadSelf,
        ProfileUpdateSelf,
        MasterScopeAccess,
        AdminAccess
    ];
}
