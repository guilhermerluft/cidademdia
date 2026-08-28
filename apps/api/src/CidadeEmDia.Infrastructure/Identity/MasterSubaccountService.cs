using System.Net.Mail;
using System.Text.Json;
using CidadeEmDia.Application.Subaccounts;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CidadeEmDia.Infrastructure.Identity;

internal sealed class MasterSubaccountService(
    AppDbContext dbContext,
    ISubaccountLimitProvider limitProvider,
    IPasswordHasher passwordHasher,
    ISubaccountInvitationEmailSender invitationEmailSender,
    SubaccountInvitationOptions invitationOptions,
    ILogger<MasterSubaccountService> logger) : IMasterSubaccountService
{
    private static readonly HashSet<string> AllowedPermissions =
        SubaccountPermissionKeys.All.ToHashSet(StringComparer.Ordinal);

    public async Task<MasterSubaccountTeam> ListAsync(
        Guid masterUserId,
        CancellationToken cancellationToken = default)
    {
        var links = await dbContext.MasterSubaccounts
            .AsNoTracking()
            .Where(x => x.MasterUserId == masterUserId)
            .Include(x => x.SubaccountUser)
                .ThenInclude(x => x.Profile)
            .Include(x => x.Permissions)
                .ThenInclude(x => x.Permission)
            .OrderBy(x => x.SubaccountUser.Profile!.DisplayName)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var invitations = await dbContext.SubaccountInvitations
            .AsNoTracking()
            .Where(x =>
                x.MasterUserId == masterUserId &&
                x.AcceptedAt == null &&
                x.RevokedAt == null &&
                x.ExpiresAt > now)
            .OrderBy(x => x.Email)
            .ToListAsync(cancellationToken);

        var limit = await limitProvider.GetLimitAsync(masterUserId, cancellationToken);
        var members = links.Select(ToMember).ToArray();
        var activeCount = links.Count(x => x.IsActive);
        var invitationSummaries = invitations.Select(ToInvitationSummary).ToArray();

        return new MasterSubaccountTeam(
            limit,
            activeCount,
            invitationSummaries.Length,
            members,
            invitationSummaries);
    }

    public async Task<MasterSubaccountResult> AddAsync(
        Guid masterUserId,
        string email,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default)
    {
        if (masterUserId == Guid.Empty || string.IsNullOrWhiteSpace(email))
            return MasterSubaccountResult.Failure("invalid_input");

        if (!TryNormalizePermissions(permissions, out var normalizedPermissions))
            return MasterSubaccountResult.Failure("invalid_permissions");

        var isMaster = await dbContext.UserRoles
            .AnyAsync(x => x.UserId == masterUserId && x.Role.Key == IdentityRoleKeys.Master, cancellationToken);
        if (!isMaster)
            return MasterSubaccountResult.Failure("master_required");

        var limit = await limitProvider.GetLimitAsync(masterUserId, cancellationToken);
        if (limit is null)
            return MasterSubaccountResult.Failure("subaccount_limit_unavailable");

        var normalizedEmail = email.Trim().ToUpperInvariant();
        var targetUser = await dbContext.Users
            .Include(x => x.Profile)
            .Include(x => x.Roles)
                .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (targetUser is null)
            return MasterSubaccountResult.Failure("subaccount_user_not_found");
        if (targetUser.Id == masterUserId)
            return MasterSubaccountResult.Failure("cannot_link_self");
        if (!targetUser.CanAuthenticate)
            return MasterSubaccountResult.Failure("subaccount_user_unavailable");
        if (targetUser.Roles.Any(x => x.Role.Key == IdentityRoleKeys.Admin || x.Role.Key == IdentityRoleKeys.Master))
            return MasterSubaccountResult.Failure("incompatible_account_role");

        var existing = await dbContext.MasterSubaccounts
            .Include(x => x.Permissions)
                .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(
                x => x.MasterUserId == masterUserId && x.SubaccountUserId == targetUser.Id,
                cancellationToken);

        if (existing?.IsActive == true)
            return MasterSubaccountResult.Failure("subaccount_already_linked");

        var activeCount = await dbContext.MasterSubaccounts
            .CountAsync(x => x.MasterUserId == masterUserId && x.Status == MasterSubaccountStatus.Active, cancellationToken);
        if (activeCount >= limit.Value)
            return MasterSubaccountResult.Failure("subaccount_limit_reached");

        var link = existing ?? new MasterSubaccount(masterUserId, targetUser.Id);
        if (existing is null)
            dbContext.MasterSubaccounts.Add(link);
        else
            link.Reactivate();

        var subaccountRole = await dbContext.Roles
            .FirstOrDefaultAsync(x => x.Key == IdentityRoleKeys.Subaccount, cancellationToken);
        if (subaccountRole is null)
            return MasterSubaccountResult.Failure("identity_catalog_unavailable");

        if (targetUser.Roles.All(x => x.Role.Key != IdentityRoleKeys.Subaccount))
            dbContext.UserRoles.Add(new UserRole(targetUser.Id, subaccountRole.Id));

        var replaceResult = await ReplacePermissionsAsync(link, normalizedPermissions, cancellationToken);
        if (!replaceResult)
            return MasterSubaccountResult.Failure("identity_catalog_unavailable");

        var pendingInvitations = await dbContext.SubaccountInvitations
            .Where(x =>
                x.MasterUserId == masterUserId &&
                x.NormalizedEmail == normalizedEmail &&
                x.AcceptedAt == null &&
                x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var invitation in pendingInvitations)
            invitation.Revoke(DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        return MasterSubaccountResult.Success(ToMember(link, targetUser, normalizedPermissions));
    }

    public async Task<SubaccountInvitationResult> InviteAsync(
        Guid masterUserId,
        string email,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default)
    {
        email = email?.Trim() ?? string.Empty;
        if (masterUserId == Guid.Empty || !IsValidEmail(email) || email.Length > 320)
            return SubaccountInvitationResult.Failure("invalid_input");

        if (!TryNormalizePermissions(permissions, out var normalizedPermissions))
            return SubaccountInvitationResult.Failure("invalid_permissions");

        var master = await dbContext.Users
            .Include(x => x.Profile)
            .Include(x => x.Roles)
                .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == masterUserId, cancellationToken);

        if (master is null || !master.Roles.Any(x => x.Role.Key == IdentityRoleKeys.Master))
            return SubaccountInvitationResult.Failure("master_required");

        var normalizedEmail = email.ToUpperInvariant();
        if (await dbContext.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken))
            return SubaccountInvitationResult.Failure("subaccount_user_already_registered");

        var limit = await limitProvider.GetLimitAsync(masterUserId, cancellationToken);
        if (limit is null)
            return SubaccountInvitationResult.Failure("subaccount_limit_unavailable");

        var now = DateTimeOffset.UtcNow;
        var activeInvitations = await dbContext.SubaccountInvitations
            .Where(x =>
                x.MasterUserId == masterUserId &&
                x.AcceptedAt == null &&
                x.RevokedAt == null &&
                x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        var sameEmailInvitations = activeInvitations
            .Where(x => x.NormalizedEmail == normalizedEmail)
            .ToArray();
        foreach (var previous in sameEmailInvitations)
            previous.Revoke(now);

        var activeLinkCount = await dbContext.MasterSubaccounts
            .CountAsync(x => x.MasterUserId == masterUserId && x.Status == MasterSubaccountStatus.Active, cancellationToken);
        var otherPendingCount = activeInvitations.Count - sameEmailInvitations.Length;
        if (activeLinkCount + otherPendingCount >= limit.Value)
            return SubaccountInvitationResult.Failure("subaccount_limit_reached");

        var rawToken = SubaccountInvitationTokenUtility.Generate();
        var invitation = new SubaccountInvitation(
            masterUserId,
            email,
            SubaccountInvitationTokenUtility.Hash(rawToken),
            JsonSerializer.Serialize(normalizedPermissions),
            now.Add(invitationOptions.TokenLifetime));

        dbContext.SubaccountInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await invitationEmailSender.SendInvitationAsync(
                email,
                master.Profile?.DisplayName ?? "Conta Master CidadeEmDia",
                rawToken,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            invitation.Revoke(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            logger.LogError(exception, "Subaccount invitation e-mail delivery failed for invitation {InvitationId}.", invitation.Id);
            return SubaccountInvitationResult.Failure("invitation_delivery_failed");
        }

        return SubaccountInvitationResult.Success(ToInvitationSummary(invitation));
    }

    public async Task<SubaccountInvitationPreview?> PreviewInvitationAsync(
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        rawToken = rawToken?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 512)
            return null;

        var hash = SubaccountInvitationTokenUtility.Hash(rawToken);
        var now = DateTimeOffset.UtcNow;
        var invitation = await dbContext.SubaccountInvitations
            .AsNoTracking()
            .Include(x => x.MasterUser)
                .ThenInclude(x => x.Profile)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (invitation is null || !invitation.IsActive(now))
            return null;

        return new SubaccountInvitationPreview(
            invitation.Email,
            invitation.MasterUser.Profile?.DisplayName ?? "Conta Master CidadeEmDia",
            DeserializePermissions(invitation.PermissionKeysJson),
            invitation.ExpiresAt);
    }

    public async Task<SubaccountInvitationAcceptResult> AcceptInvitationAsync(
        string rawToken,
        string password,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        rawToken = rawToken?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 512 || password is null || password.Length < 8 || password.Length > 128 || displayName.Length is < 2 or > 160)
            return SubaccountInvitationAcceptResult.Failure("invalid_input");

        var now = DateTimeOffset.UtcNow;
        var hash = SubaccountInvitationTokenUtility.Hash(rawToken);
        var invitation = await dbContext.SubaccountInvitations
            .Include(x => x.MasterUser)
                .ThenInclude(x => x.Roles)
                    .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (invitation is null || !invitation.IsActive(now))
            return SubaccountInvitationAcceptResult.Failure("invalid_or_expired_invitation");

        if (!invitation.MasterUser.CanAuthenticate || !invitation.MasterUser.Roles.Any(x => x.Role.Key == IdentityRoleKeys.Master))
            return SubaccountInvitationAcceptResult.Failure("master_unavailable");

        if (await dbContext.Users.AnyAsync(x => x.NormalizedEmail == invitation.NormalizedEmail, cancellationToken))
            return SubaccountInvitationAcceptResult.Failure("email_already_registered");

        var limit = await limitProvider.GetLimitAsync(invitation.MasterUserId, cancellationToken);
        if (limit is null)
            return SubaccountInvitationAcceptResult.Failure("subaccount_limit_unavailable");

        var activeLinkCount = await dbContext.MasterSubaccounts
            .CountAsync(x => x.MasterUserId == invitation.MasterUserId && x.Status == MasterSubaccountStatus.Active, cancellationToken);
        if (activeLinkCount >= limit.Value)
            return SubaccountInvitationAcceptResult.Failure("subaccount_limit_reached");

        var roles = await dbContext.Roles
            .Where(x => x.Key == IdentityRoleKeys.Citizen || x.Key == IdentityRoleKeys.Subaccount)
            .ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, cancellationToken);
        if (!roles.TryGetValue(IdentityRoleKeys.Citizen, out var citizenRole) ||
            !roles.TryGetValue(IdentityRoleKeys.Subaccount, out var subaccountRole))
            return SubaccountInvitationAcceptResult.Failure("identity_catalog_unavailable");

        var permissionKeys = DeserializePermissions(invitation.PermissionKeysJson);
        if (!TryNormalizePermissions(permissionKeys, out var normalizedPermissions))
            return SubaccountInvitationAcceptResult.Failure("invalid_permissions");

        var user = new User(invitation.Email, passwordHasher.Hash(password));
        var profile = new UserProfile(user.Id, displayName);
        var link = new MasterSubaccount(invitation.MasterUserId, user.Id);

        dbContext.Users.Add(user);
        dbContext.UserProfiles.Add(profile);
        dbContext.UserRoles.Add(new UserRole(user.Id, citizenRole.Id));
        dbContext.UserRoles.Add(new UserRole(user.Id, subaccountRole.Id));
        dbContext.MasterSubaccounts.Add(link);

        var replaceResult = await ReplacePermissionsAsync(link, normalizedPermissions, cancellationToken);
        if (!replaceResult)
            return SubaccountInvitationAcceptResult.Failure("identity_catalog_unavailable");

        invitation.Accept(now);
        await dbContext.SaveChangesAsync(cancellationToken);

        return SubaccountInvitationAcceptResult.Success();
    }

    public async Task<MasterSubaccountResult> UpdatePermissionsAsync(
        Guid masterUserId,
        Guid linkId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizePermissions(permissions, out var normalizedPermissions))
            return MasterSubaccountResult.Failure("invalid_permissions");

        var link = await dbContext.MasterSubaccounts
            .Where(x => x.Id == linkId && x.MasterUserId == masterUserId)
            .Include(x => x.SubaccountUser)
                .ThenInclude(x => x.Profile)
            .Include(x => x.Permissions)
                .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(cancellationToken);

        if (link is null)
            return MasterSubaccountResult.Failure("subaccount_not_found");
        if (!link.IsActive)
            return MasterSubaccountResult.Failure("subaccount_revoked");

        var replaceResult = await ReplacePermissionsAsync(link, normalizedPermissions, cancellationToken);
        if (!replaceResult)
            return MasterSubaccountResult.Failure("identity_catalog_unavailable");

        await dbContext.SaveChangesAsync(cancellationToken);
        return MasterSubaccountResult.Success(ToMember(link));
    }

    public async Task<MasterSubaccountResult> RevokeAsync(
        Guid masterUserId,
        Guid linkId,
        CancellationToken cancellationToken = default)
    {
        var link = await dbContext.MasterSubaccounts
            .Where(x => x.Id == linkId && x.MasterUserId == masterUserId)
            .Include(x => x.SubaccountUser)
                .ThenInclude(x => x.Profile)
            .Include(x => x.Permissions)
                .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(cancellationToken);

        if (link is null)
            return MasterSubaccountResult.Failure("subaccount_not_found");

        link.Revoke(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MasterSubaccountResult.Success(ToMember(link));
    }

    public async Task<SubaccountContext?> GetContextAsync(
        Guid subaccountUserId,
        Guid masterUserId,
        CancellationToken cancellationToken = default)
    {
        var link = await dbContext.MasterSubaccounts
            .AsNoTracking()
            .Where(x =>
                x.MasterUserId == masterUserId &&
                x.SubaccountUserId == subaccountUserId &&
                x.Status == MasterSubaccountStatus.Active)
            .Include(x => x.Permissions)
                .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(cancellationToken);

        if (link is null)
            return null;

        var permissions = link.Permissions
            .Select(x => x.Permission.Key)
            .Where(AllowedPermissions.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return new SubaccountContext(link.Id, masterUserId, permissions);
    }

    public Task<bool> HasPermissionAsync(
        Guid subaccountUserId,
        Guid masterUserId,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedPermissions.Contains(permissionKey))
            return Task.FromResult(false);

        return dbContext.MasterSubaccountPermissions.AnyAsync(x =>
            x.MasterSubaccount.SubaccountUserId == subaccountUserId &&
            x.MasterSubaccount.MasterUserId == masterUserId &&
            x.MasterSubaccount.Status == MasterSubaccountStatus.Active &&
            x.Permission.Key == permissionKey,
            cancellationToken);
    }

    private async Task<bool> ReplacePermissionsAsync(
        MasterSubaccount link,
        IReadOnlyCollection<string> requestedKeys,
        CancellationToken cancellationToken)
    {
        var requested = requestedKeys.ToHashSet(StringComparer.Ordinal);
        var existing = link.Permissions
            .Where(x => x.Permission is not null)
            .ToDictionary(x => x.Permission.Key, StringComparer.Ordinal);

        foreach (var permission in existing.Where(x => !requested.Contains(x.Key)).Select(x => x.Value).ToArray())
            dbContext.MasterSubaccountPermissions.Remove(permission);

        var missingKeys = requested.Where(x => !existing.ContainsKey(x)).ToArray();
        if (missingKeys.Length == 0)
            return true;

        var permissionEntities = await dbContext.Permissions
            .Where(x => missingKeys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, cancellationToken);

        if (permissionEntities.Count != missingKeys.Length)
            return false;

        foreach (var key in missingKeys)
            dbContext.MasterSubaccountPermissions.Add(new MasterSubaccountPermission(link.Id, permissionEntities[key].Id));

        return true;
    }

    private static bool TryNormalizePermissions(
        IReadOnlyCollection<string> permissions,
        out string[] normalized)
    {
        normalized = permissions
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return normalized.All(AllowedPermissions.Contains);
    }

    private static MasterSubaccountMember ToMember(MasterSubaccount link)
    {
        var permissions = link.Permissions
            .Select(x => x.Permission.Key)
            .Where(AllowedPermissions.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return ToMember(link, link.SubaccountUser, permissions);
    }

    private static MasterSubaccountMember ToMember(
        MasterSubaccount link,
        User subaccountUser,
        IReadOnlyCollection<string> permissions) =>
        new(
            link.Id,
            subaccountUser.Id,
            subaccountUser.Email,
            subaccountUser.Profile?.DisplayName ?? subaccountUser.Email,
            link.Status.ToString().ToUpperInvariant(),
            permissions);

    private static SubaccountInvitationSummary ToInvitationSummary(SubaccountInvitation invitation) =>
        new(
            invitation.Id,
            invitation.Email,
            DeserializePermissions(invitation.PermissionKeysJson),
            invitation.ExpiresAt);

    private static string[] DeserializePermissions(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var parsed = new MailAddress(email);
            return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
