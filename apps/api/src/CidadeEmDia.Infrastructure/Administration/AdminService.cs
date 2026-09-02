using CidadeEmDia.Application.Administration;
using CidadeEmDia.Domain.Administration;
using CidadeEmDia.Domain.Billing;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CidadeEmDia.Infrastructure.Administration;

internal sealed class AdminService(AppDbContext dbContext) : IAdminService
{
    private const int MaxPageSize = 50;

    public async Task<AdminResult<AdminOverview>> GetOverviewAsync(
        Guid requesterUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsActiveAdminAsync(requesterUserId, cancellationToken))
            return AdminResult<AdminOverview>.Failure("admin_access_denied");

        var users = await dbContext.Users.AsNoTracking().CountAsync(cancellationToken);
        var activeUsers = await dbContext.Users.AsNoTracking()
            .CountAsync(user => user.Status == UserStatus.Active, cancellationToken);
        var suspendedUsers = await dbContext.Users.AsNoTracking()
            .CountAsync(user => user.Status == UserStatus.Suspended, cancellationToken);
        var blockedUsers = await dbContext.Users.AsNoTracking()
            .CountAsync(user => user.Status == UserStatus.Blocked, cancellationToken);
        var masters = await dbContext.UserRoles.AsNoTracking()
            .CountAsync(link => link.Role.Key == IdentityRoleKeys.Master, cancellationToken);
        var subaccounts = await dbContext.UserRoles.AsNoTracking()
            .CountAsync(link => link.Role.Key == IdentityRoleKeys.Subaccount, cancellationToken);
        var institutions = await dbContext.Institutions.AsNoTracking().CountAsync(cancellationToken);
        var occurrences = await dbContext.Occurrences.AsNoTracking().CountAsync(cancellationToken);
        var posts = await dbContext.Posts.AsNoTracking().CountAsync(cancellationToken);
        var activeSubscriptions = await dbContext.Subscriptions.AsNoTracking()
            .CountAsync(subscription => subscription.Status == SubscriptionStatus.Active, cancellationToken);
        var payments = await dbContext.BillingPayments.AsNoTracking().CountAsync(cancellationToken);

        return AdminResult<AdminOverview>.Success(new AdminOverview(
            users,
            activeUsers,
            suspendedUsers,
            blockedUsers,
            masters,
            subaccounts,
            institutions,
            occurrences,
            posts,
            activeSubscriptions,
            payments,
            DateTimeOffset.UtcNow));
    }

    public async Task<AdminResult<AdminPage<AdminUserItem>>> ListUsersAsync(
        Guid requesterUserId,
        string? search,
        string? status,
        string? role,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!await IsActiveAdminAsync(requesterUserId, cancellationToken))
            return AdminResult<AdminPage<AdminUserItem>>.Failure("admin_access_denied");

        var (normalizedPage, normalizedPageSize) = NormalizePage(page, pageSize);
        var query = dbContext.Users
            .AsNoTracking()
            .Include(user => user.Profile)
            .Include(user => user.Roles)
                .ThenInclude(link => link.Role)
            .AsQueryable();

        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(user =>
                EF.Functions.ILike(user.Email, pattern)
                || (user.Profile != null && EF.Functions.ILike(user.Profile.DisplayName, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<UserStatus>(status.Trim(), ignoreCase: true, out var parsedStatus))
                return AdminResult<AdminPage<AdminUserItem>>.Failure("admin_user_status_filter_invalid");

            query = query.Where(user => user.Status == parsedStatus);
        }

        var normalizedRole = role?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedRole))
            query = query.Where(user => user.Roles.Any(link => link.Role.Key == normalizedRole));

        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(user => user.Profile != null ? user.Profile.DisplayName : user.Email)
            .ThenBy(user => user.Email)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return AdminResult<AdminPage<AdminUserItem>>.Success(new AdminPage<AdminUserItem>(
            users.Select(ToUserItem).ToArray(),
            normalizedPage,
            normalizedPageSize,
            total));
    }

    public async Task<AdminResult<AdminUserStatusChange>> ChangeUserStatusAsync(
        Guid requesterUserId,
        Guid userId,
        string status,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!await IsActiveAdminAsync(requesterUserId, cancellationToken))
            return AdminResult<AdminUserStatusChange>.Failure("admin_access_denied");

        if (requesterUserId == userId)
            return AdminResult<AdminUserStatusChange>.Failure("admin_self_status_change_not_allowed");

        if (!TryParseManagedStatus(status, out var targetStatus))
            return AdminResult<AdminUserStatusChange>.Failure("admin_user_status_invalid");

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
            return AdminResult<AdminUserStatusChange>.Failure("admin_reason_required");
        if (normalizedReason.Length > 500)
            return AdminResult<AdminUserStatusChange>.Failure("admin_reason_too_long");

        var user = await dbContext.Users
            .Include(target => target.Profile)
            .Include(target => target.Roles)
                .ThenInclude(link => link.Role)
            .Include(target => target.RefreshTokens)
            .FirstOrDefaultAsync(target => target.Id == userId, cancellationToken);

        if (user is null)
            return AdminResult<AdminUserStatusChange>.Failure("admin_user_not_found");

        if (user.Roles.Any(link => link.Role.Key == IdentityRoleKeys.Admin))
            return AdminResult<AdminUserStatusChange>.Failure("admin_target_is_admin");

        if (user.Status == targetStatus)
            return AdminResult<AdminUserStatusChange>.Success(new AdminUserStatusChange(ToUserItem(user), false));

        var previousStatus = user.Status;
        var now = DateTimeOffset.UtcNow;

        switch (targetStatus)
        {
            case UserStatus.Active:
                user.Activate();
                break;
            case UserStatus.Suspended:
                user.Suspend();
                break;
            case UserStatus.Blocked:
                user.Block();
                break;
            default:
                return AdminResult<AdminUserStatusChange>.Failure("admin_user_status_invalid");
        }

        if (targetStatus is UserStatus.Suspended or UserStatus.Blocked)
        {
            foreach (var refreshToken in user.RefreshTokens.Where(token => token.IsActive(now)))
                refreshToken.Revoke(now, "admin_user_status_changed");
        }

        dbContext.AdminAuditLogs.Add(new AdminAuditLog(
            requesterUserId,
            AdminAuditActionKeys.UserStatusChanged,
            AdminAuditEntityKeys.User,
            user.Id,
            previousStatus.ToString(),
            user.Status.ToString(),
            normalizedReason,
            now));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            return AdminResult<AdminUserStatusChange>.Failure(
                "admin_persistence_conflict",
                exception.InnerException?.Message);
        }

        return AdminResult<AdminUserStatusChange>.Success(new AdminUserStatusChange(ToUserItem(user), true));
    }

    public async Task<AdminResult<AdminPage<AdminInstitutionItem>>> ListInstitutionsAsync(
        Guid requesterUserId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!await IsActiveAdminAsync(requesterUserId, cancellationToken))
            return AdminResult<AdminPage<AdminInstitutionItem>>.Failure("admin_access_denied");

        var (normalizedPage, normalizedPageSize) = NormalizePage(page, pageSize);
        var query = dbContext.Institutions.AsNoTracking().AsQueryable();
        var normalizedSearch = search?.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(institution =>
                EF.Functions.ILike(institution.Name, pattern)
                || EF.Functions.ILike(institution.Slug, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(institution => institution.Name)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(institution => new AdminInstitutionItem(
                institution.Id,
                institution.Name,
                institution.Slug,
                institution.Type,
                institution.ScopeLevel,
                institution.Status,
                institution.OfficialEmail,
                institution.StateCode,
                institution.Representatives.Count,
                institution.Memberships.Count,
                institution.CreatedAt,
                institution.UpdatedAt))
            .ToListAsync(cancellationToken);

        return AdminResult<AdminPage<AdminInstitutionItem>>.Success(new AdminPage<AdminInstitutionItem>(
            rows,
            normalizedPage,
            normalizedPageSize,
            total));
    }

    public async Task<AdminResult<AdminPage<AdminOccurrenceItem>>> ListOccurrencesAsync(
        Guid requesterUserId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!await IsActiveAdminAsync(requesterUserId, cancellationToken))
            return AdminResult<AdminPage<AdminOccurrenceItem>>.Failure("admin_access_denied");

        var (normalizedPage, normalizedPageSize) = NormalizePage(page, pageSize);
        var query = dbContext.Occurrences.AsNoTracking().AsQueryable();
        var normalizedSearch = search?.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(occurrence =>
                EF.Functions.ILike(occurrence.Title, pattern)
                || EF.Functions.ILike(occurrence.AddressText, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(occurrence => occurrence.CreatedAt)
            .ThenByDescending(occurrence => occurrence.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(occurrence => new AdminOccurrenceItem(
            occurrence.Id,
            occurrence.PublicCode.Value,
            occurrence.AuthorUserId,
            occurrence.Title,
            occurrence.Status.Value,
            occurrence.AddressText,
            occurrence.StateCode,
            occurrence.CreatedAt,
            occurrence.UpdatedAt,
            occurrence.ClosedAt,
            occurrence.CancelledAt)).ToArray();

        return AdminResult<AdminPage<AdminOccurrenceItem>>.Success(new AdminPage<AdminOccurrenceItem>(
            items,
            normalizedPage,
            normalizedPageSize,
            total));
    }

    public async Task<AdminResult<AdminPage<AdminPostItem>>> ListPostsAsync(
        Guid requesterUserId,
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!await IsActiveAdminAsync(requesterUserId, cancellationToken))
            return AdminResult<AdminPage<AdminPostItem>>.Failure("admin_access_denied");

        var (normalizedPage, normalizedPageSize) = NormalizePage(page, pageSize);
        var query = dbContext.Posts.AsNoTracking().AsQueryable();
        var normalizedSearch = search?.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(post =>
                (post.Title != null && EF.Functions.ILike(post.Title, pattern))
                || (post.Body != null && EF.Functions.ILike(post.Body, pattern)));
        }

        var normalizedStatus = status?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            if (normalizedStatus is not ("draft" or "published" or "archived"))
                return AdminResult<AdminPage<AdminPostItem>>.Failure("admin_post_status_filter_invalid");

            query = query.Where(post => post.Status == normalizedStatus);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(post => post.CreatedAt)
            .ThenByDescending(post => post.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(post => new AdminPostItem(
                post.Id,
                post.PublisherUserId,
                post.MasterUserId,
                post.Type,
                post.Status,
                post.Title,
                post.Media.Count,
                post.Placements.Count,
                post.CreatedAt,
                post.UpdatedAt,
                post.PublishedAt,
                post.ArchivedAt))
            .ToListAsync(cancellationToken);

        return AdminResult<AdminPage<AdminPostItem>>.Success(new AdminPage<AdminPostItem>(
            rows,
            normalizedPage,
            normalizedPageSize,
            total));
    }

    public async Task<AdminResult<AdminBillingSnapshot>> GetBillingAsync(
        Guid requesterUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!await IsActiveAdminAsync(requesterUserId, cancellationToken))
            return AdminResult<AdminBillingSnapshot>.Failure("admin_access_denied");

        var (normalizedPage, normalizedPageSize) = NormalizePage(page, pageSize);
        var now = DateTimeOffset.UtcNow;

        var versions = await dbContext.PlanVersions
            .AsNoTracking()
            .Include(version => version.PlanOffer)
                .ThenInclude(offer => offer.Plan)
            .Include(version => version.PlanOffer)
                .ThenInclude(offer => offer.Category)
            .OrderBy(version => version.PlanOffer.Plan.Key)
            .ThenBy(version => version.PlanOffer.Category.BillingIntervalMonths)
            .ThenByDescending(version => version.Version)
            .ToListAsync(cancellationToken);

        var plans = versions
            .GroupBy(version => version.PlanOfferId)
            .Select(group => group.First())
            .Select(version => new AdminPlanItem(
                version.Id,
                version.PlanOffer.Plan.Key,
                version.PlanOffer.Plan.Name,
                version.PlanOffer.Key,
                version.PlanOffer.Category.Key,
                version.PlanOffer.Category.Name,
                version.PlanOffer.Category.BillingIntervalMonths,
                version.Version,
                version.PriceCents,
                version.SignupFeeCents,
                version.SubaccountLimit,
                version.MonthlyPublicationLimit,
                version.EffectiveFrom,
                version.EffectiveTo))
            .ToArray();

        var subscriptionQuery = dbContext.Subscriptions
            .AsNoTracking()
            .Include(subscription => subscription.MasterUser)
                .ThenInclude(user => user.Profile)
            .Include(subscription => subscription.PlanVersion)
                .ThenInclude(version => version.PlanOffer)
                    .ThenInclude(offer => offer.Plan)
            .Include(subscription => subscription.PlanVersion)
                .ThenInclude(version => version.PlanOffer)
                    .ThenInclude(offer => offer.Category)
            .Include(subscription => subscription.UsageCounters)
            .AsSplitQuery();

        var subscriptionTotal = await subscriptionQuery.CountAsync(cancellationToken);
        var subscriptions = await subscriptionQuery
            .OrderByDescending(subscription => subscription.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var subscriptionItems = subscriptions.Select(subscription =>
        {
            var currentUsage = subscription.UsageCounters
                .Where(counter => counter.WindowStart <= now && now < counter.WindowEnd)
                .OrderByDescending(counter => counter.WindowStart)
                .FirstOrDefault()?.PublicationCount ?? 0;

            return new AdminSubscriptionItem(
                subscription.Id,
                subscription.MasterUserId,
                subscription.MasterUser.Email,
                subscription.MasterUser.Profile?.DisplayName ?? subscription.MasterUser.Email,
                subscription.Status.ToString(),
                subscription.PlanVersion.PlanOffer.Plan.Key,
                subscription.PlanVersion.PlanOffer.Plan.Name,
                subscription.PlanVersion.PlanOffer.Key,
                subscription.PlanVersion.Version,
                subscription.PlanVersion.SubaccountLimit,
                subscription.PlanVersion.MonthlyPublicationLimit,
                currentUsage,
                subscription.CurrentPeriodStart,
                subscription.CurrentPeriodEnd,
                subscription.CancelAtPeriodEnd,
                subscription.PastDueAt,
                subscription.GracePeriodEndsAt,
                subscription.CanceledAt);
        }).ToArray();

        var paymentQuery = dbContext.BillingPayments
            .AsNoTracking()
            .Include(payment => payment.Subscription)
                .ThenInclude(subscription => subscription.MasterUser)
                    .ThenInclude(user => user.Profile);

        var paymentTotal = await paymentQuery.CountAsync(cancellationToken);
        var payments = await paymentQuery
            .OrderByDescending(payment => payment.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var paymentItems = payments.Select(payment => new AdminPaymentItem(
            payment.Id,
            payment.SubscriptionId,
            payment.Subscription.MasterUserId,
            payment.Subscription.MasterUser.Email,
            payment.Provider,
            payment.ProviderPaymentId,
            payment.AmountCents,
            payment.Currency,
            payment.Status,
            payment.StatusDetail,
            payment.ApprovedAt,
            payment.CreatedAt)).ToArray();

        return AdminResult<AdminBillingSnapshot>.Success(new AdminBillingSnapshot(
            plans,
            new AdminPage<AdminSubscriptionItem>(subscriptionItems, normalizedPage, normalizedPageSize, subscriptionTotal),
            new AdminPage<AdminPaymentItem>(paymentItems, normalizedPage, normalizedPageSize, paymentTotal)));
    }

    public async Task<AdminResult<AdminPage<AdminAuditLogItem>>> ListAuditLogsAsync(
        Guid requesterUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!await IsActiveAdminAsync(requesterUserId, cancellationToken))
            return AdminResult<AdminPage<AdminAuditLogItem>>.Failure("admin_access_denied");

        var (normalizedPage, normalizedPageSize) = NormalizePage(page, pageSize);
        var query = dbContext.AdminAuditLogs.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var logs = await query
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var actorIds = logs.Select(log => log.ActorUserId).Distinct().ToArray();
        var actors = await dbContext.Users
            .AsNoTracking()
            .Include(user => user.Profile)
            .Where(user => actorIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        var items = logs.Select(log =>
        {
            actors.TryGetValue(log.ActorUserId, out var actor);
            return new AdminAuditLogItem(
                log.Id,
                log.ActorUserId,
                actor?.Email,
                actor?.Profile?.DisplayName,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.PreviousValue,
                log.NewValue,
                log.Reason,
                log.OccurredAt);
        }).ToArray();

        return AdminResult<AdminPage<AdminAuditLogItem>>.Success(new AdminPage<AdminAuditLogItem>(
            items,
            normalizedPage,
            normalizedPageSize,
            total));
    }

    private Task<bool> IsActiveAdminAsync(Guid requesterUserId, CancellationToken cancellationToken) =>
        requesterUserId != Guid.Empty
            ? dbContext.Users.AsNoTracking().AnyAsync(
                user => user.Id == requesterUserId
                    && user.Status == UserStatus.Active
                    && user.Roles.Any(link => link.Role.Key == IdentityRoleKeys.Admin),
                cancellationToken)
            : Task.FromResult(false);

    private static AdminUserItem ToUserItem(User user) => new(
        user.Id,
        user.Email,
        user.Profile?.DisplayName ?? user.Email,
        user.Status.ToString(),
        user.Roles.Select(link => link.Role.Key).OrderBy(key => key).ToArray(),
        user.EmailConfirmedAt.HasValue,
        user.LastLoginAt,
        user.CreatedAt,
        user.UpdatedAt);

    private static bool TryParseManagedStatus(string? status, out UserStatus parsed)
    {
        parsed = UserStatus.Active;
        if (!Enum.TryParse<UserStatus>(status?.Trim(), ignoreCase: true, out var candidate))
            return false;
        if (candidate is not (UserStatus.Active or UserStatus.Suspended or UserStatus.Blocked))
            return false;
        parsed = candidate;
        return true;
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));
}
