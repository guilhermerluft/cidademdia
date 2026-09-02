namespace CidadeEmDia.Application.Administration;

public interface IAdminService
{
    Task<AdminResult<AdminOverview>> GetOverviewAsync(
        Guid requesterUserId,
        CancellationToken cancellationToken = default);

    Task<AdminResult<AdminPage<AdminUserItem>>> ListUsersAsync(
        Guid requesterUserId,
        string? search,
        string? status,
        string? role,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminResult<AdminUserStatusChange>> ChangeUserStatusAsync(
        Guid requesterUserId,
        Guid userId,
        string status,
        string reason,
        CancellationToken cancellationToken = default);

    Task<AdminResult<AdminPage<AdminInstitutionItem>>> ListInstitutionsAsync(
        Guid requesterUserId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminResult<AdminPage<AdminOccurrenceItem>>> ListOccurrencesAsync(
        Guid requesterUserId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminResult<AdminPage<AdminPostItem>>> ListPostsAsync(
        Guid requesterUserId,
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminResult<AdminBillingSnapshot>> GetBillingAsync(
        Guid requesterUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminResult<AdminPage<AdminAuditLogItem>>> ListAuditLogsAsync(
        Guid requesterUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

public sealed record AdminResult<T>(
    bool Succeeded,
    T? Data,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static AdminResult<T> Success(T data) => new(true, data);
    public static AdminResult<T> Failure(string errorCode, string? errorDetail = null) =>
        new(false, default, errorCode, errorDetail);
}

public sealed record AdminPage<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems);

public sealed record AdminOverview(
    int Users,
    int ActiveUsers,
    int SuspendedUsers,
    int BlockedUsers,
    int Masters,
    int Subaccounts,
    int Institutions,
    int Occurrences,
    int Posts,
    int ActiveSubscriptions,
    int Payments,
    DateTimeOffset GeneratedAt);

public sealed record AdminUserItem(
    Guid Id,
    string Email,
    string DisplayName,
    string Status,
    IReadOnlyList<string> Roles,
    bool EmailConfirmed,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminUserStatusChange(
    AdminUserItem User,
    bool Changed);

public sealed record AdminInstitutionItem(
    Guid Id,
    string Name,
    string Slug,
    string Type,
    string ScopeLevel,
    string Status,
    string? OfficialEmail,
    string? StateCode,
    int Representatives,
    int Memberships,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminOccurrenceItem(
    Guid Id,
    string PublicCode,
    Guid AuthorUserId,
    string Title,
    string Status,
    string AddressText,
    string? StateCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? CancelledAt);

public sealed record AdminPostItem(
    Guid Id,
    Guid PublisherUserId,
    Guid? MasterUserId,
    string Type,
    string Status,
    string? Title,
    int MediaCount,
    int PlacementCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt);

public sealed record AdminPlanItem(
    Guid PlanVersionId,
    string PlanKey,
    string PlanName,
    string OfferKey,
    string BillingCategoryKey,
    string BillingCategoryName,
    int BillingIntervalMonths,
    int Version,
    long PriceCents,
    long SignupFeeCents,
    int SubaccountLimit,
    int MonthlyPublicationLimit,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public sealed record AdminSubscriptionItem(
    Guid Id,
    Guid MasterUserId,
    string MasterEmail,
    string MasterDisplayName,
    string Status,
    string PlanKey,
    string PlanName,
    string OfferKey,
    int PlanVersion,
    int SubaccountLimit,
    int MonthlyPublicationLimit,
    int CurrentPublicationCount,
    DateTimeOffset CurrentPeriodStart,
    DateTimeOffset CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    DateTimeOffset? PastDueAt,
    DateTimeOffset? GracePeriodEndsAt,
    DateTimeOffset? CanceledAt);

public sealed record AdminPaymentItem(
    Guid Id,
    Guid SubscriptionId,
    Guid MasterUserId,
    string MasterEmail,
    string Provider,
    string ProviderPaymentId,
    long AmountCents,
    string Currency,
    string Status,
    string? StatusDetail,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset CreatedAt);

public sealed record AdminBillingSnapshot(
    IReadOnlyList<AdminPlanItem> Plans,
    AdminPage<AdminSubscriptionItem> Subscriptions,
    AdminPage<AdminPaymentItem> Payments);

public sealed record AdminAuditLogItem(
    Guid Id,
    Guid ActorUserId,
    string? ActorEmail,
    string? ActorDisplayName,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? PreviousValue,
    string? NewValue,
    string Reason,
    DateTimeOffset OccurredAt);
