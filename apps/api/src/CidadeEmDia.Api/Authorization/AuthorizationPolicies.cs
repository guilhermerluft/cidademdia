using CidadeEmDia.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace CidadeEmDia.Api.Authorization;

public static class AuthorizationPolicies
{
    public const string ProfileRead = "profile.read";
    public const string ProfileUpdate = "profile.update";
    public const string MasterScope = "master.scope";
    public const string AdminAccess = "admin.access";

    public static IServiceCollection AddCidadeEmDiaAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(ProfileRead, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(IdentityClaimTypes.Permission, IdentityPermissionKeys.ProfileReadSelf));

            options.AddPolicy(ProfileUpdate, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(IdentityClaimTypes.Permission, IdentityPermissionKeys.ProfileUpdateSelf));

            options.AddPolicy(MasterScope, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(IdentityClaimTypes.Permission, IdentityPermissionKeys.MasterScopeAccess));

            options.AddPolicy(AdminAccess, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(IdentityRoleKeys.Admin)
                .RequireClaim(IdentityClaimTypes.Permission, IdentityPermissionKeys.AdminAccess));
        });

        return services;
    }
}
