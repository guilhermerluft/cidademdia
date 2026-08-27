using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CidadeEmDia.Infrastructure.Identity;

internal sealed class IdentitySeedHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<IdentitySeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var roles = (await dbContext.Roles.ToListAsync(cancellationToken))
            .ToDictionary(x => x.Key, StringComparer.Ordinal);

        foreach (var definition in IdentitySeedCatalog.Roles)
        {
            if (roles.ContainsKey(definition.Key))
                continue;

            var role = new Role(definition.Key, definition.Name);
            dbContext.Roles.Add(role);
            roles.Add(role.Key, role);
        }

        var permissions = (await dbContext.Permissions.ToListAsync(cancellationToken))
            .ToDictionary(x => x.Key, StringComparer.Ordinal);

        foreach (var definition in IdentitySeedCatalog.Permissions)
        {
            if (permissions.ContainsKey(definition.Key))
                continue;

            var permission = new Permission(definition.Key, definition.Description);
            dbContext.Permissions.Add(permission);
            permissions.Add(permission.Key, permission);
        }

        var existingPairs = (await dbContext.RolePermissions
                .Select(x => new { x.RoleId, x.PermissionId })
                .ToListAsync(cancellationToken))
            .Select(x => (x.RoleId, x.PermissionId))
            .ToHashSet();

        foreach (var roleDefinition in IdentitySeedCatalog.Roles)
        {
            var role = roles[roleDefinition.Key];
            foreach (var permissionKey in roleDefinition.Permissions)
            {
                var permission = permissions[permissionKey];
                if (!existingPairs.Add((role.Id, permission.Id)))
                    continue;

                dbContext.RolePermissions.Add(new RolePermission(role.Id, permission.Id));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Identity seed ensured with {RoleCount} roles and {PermissionCount} permissions.", roles.Count, permissions.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
