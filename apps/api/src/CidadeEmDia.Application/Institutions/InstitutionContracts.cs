namespace CidadeEmDia.Application.Institutions;

public sealed record InstitutionJurisdictionInput(
    string JurisdictionType,
    Guid? CityId,
    string? StateCode,
    string? CustomAreaLabel);

public sealed record CreateInstitutionCommand(
    string Name,
    string Slug,
    string Type,
    string ScopeLevel,
    string? Cnpj,
    string? OfficialEmail,
    string? OfficialDomain,
    string? Description,
    Guid? CityId,
    string? StateCode,
    IReadOnlyCollection<InstitutionJurisdictionInput> Jurisdictions);

public sealed record CreateRepresentativeCommand(
    string Name,
    string Slug,
    string PublicRole,
    string? OfficialEmail,
    Guid? PhotoMediaId,
    DateOnly? MandateStart,
    DateOnly? MandateEnd,
    int DisplayOrder);

public sealed record CreateInstitutionInviteCommand(
    Guid? RepresentativeId,
    string? ExpectedEmail,
    int ExpiresInHours = 72);

public sealed record InstitutionJurisdictionItem(
    Guid Id,
    string JurisdictionType,
    Guid? CityId,
    string? StateCode,
    string? CustomAreaLabel);

public sealed record InstitutionRepresentativeItem(
    Guid Id,
    Guid InstitutionId,
    string Name,
    string Slug,
    string PublicRole,
    string? OfficialEmail,
    Guid? PhotoMediaId,
    DateOnly? MandateStart,
    DateOnly? MandateEnd,
    Guid? AccountId,
    string ProfileStatus,
    int DisplayOrder);

public sealed record InstitutionItem(
    Guid Id,
    string Name,
    string Slug,
    string Type,
    string ScopeLevel,
    string? OfficialEmail,
    string? OfficialDomain,
    string? Description,
    Guid? LogoMediaId,
    Guid? CityId,
    string? StateCode,
    string Status,
    IReadOnlyCollection<InstitutionJurisdictionItem> Jurisdictions,
    IReadOnlyCollection<InstitutionRepresentativeItem> Representatives);

public sealed record InstitutionDirectoryPage(
    IReadOnlyCollection<InstitutionItem> Items,
    int Page,
    int PageSize,
    int TotalItems);

public sealed record MasterDirectoryInstitutionItem(
    Guid InstitutionId,
    string Name,
    string Type,
    string ScopeLevel,
    string? StateCode,
    string? PublicRole);

public sealed record MasterDirectoryItem(
    Guid Id,
    string DisplayName,
    Guid? AvatarMediaId,
    IReadOnlyCollection<MasterDirectoryInstitutionItem> Institutions);

public sealed record MasterDirectoryPage(
    IReadOnlyCollection<MasterDirectoryItem> Items,
    int Page,
    int PageSize,
    int TotalItems);

public sealed record InstitutionOperationResult(
    bool Succeeded,
    InstitutionItem? Institution = null,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static InstitutionOperationResult Success(InstitutionItem institution) =>
        new(true, institution);

    public static InstitutionOperationResult Failure(
        string errorCode,
        string? detail = null) =>
        new(false, null, errorCode, detail);
}

public sealed record RepresentativeOperationResult(
    bool Succeeded,
    InstitutionRepresentativeItem? Representative = null,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static RepresentativeOperationResult Success(InstitutionRepresentativeItem representative) =>
        new(true, representative);

    public static RepresentativeOperationResult Failure(
        string errorCode,
        string? detail = null) =>
        new(false, null, errorCode, detail);
}

public sealed record InstitutionInviteCreatedItem(
    Guid Id,
    Guid InstitutionId,
    Guid? RepresentativeId,
    string? ExpectedEmail,
    string Token,
    DateTimeOffset ExpiresAt);

public sealed record InstitutionInviteCreateResult(
    bool Succeeded,
    InstitutionInviteCreatedItem? Invite = null,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static InstitutionInviteCreateResult Success(InstitutionInviteCreatedItem invite) =>
        new(true, invite);

    public static InstitutionInviteCreateResult Failure(
        string errorCode,
        string? detail = null) =>
        new(false, null, errorCode, detail);
}

public sealed record InstitutionMembershipItem(
    Guid Id,
    Guid InstitutionId,
    Guid UserId,
    Guid? RepresentativeId,
    string MembershipRole,
    string Status,
    DateTimeOffset JoinedAt);

public sealed record InstitutionInviteClaimResult(
    bool Succeeded,
    InstitutionMembershipItem? Membership = null,
    InstitutionRepresentativeItem? Representative = null,
    bool WasChanged = false,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static InstitutionInviteClaimResult Success(
        InstitutionMembershipItem membership,
        InstitutionRepresentativeItem? representative,
        bool wasChanged) =>
        new(true, membership, representative, wasChanged);

    public static InstitutionInviteClaimResult Failure(
        string errorCode,
        string? detail = null) =>
        new(false, null, null, false, errorCode, detail);
}

public interface IInstitutionService
{
    Task<InstitutionDirectoryPage> ListAsync(
        string? search = null,
        string? type = null,
        string? stateCode = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<MasterDirectoryPage> ListActiveMastersAsync(
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<InstitutionOperationResult> GetAsync(
        Guid institutionId,
        CancellationToken cancellationToken = default);

    Task<InstitutionOperationResult> CreateAsync(
        Guid requesterUserId,
        CreateInstitutionCommand command,
        CancellationToken cancellationToken = default);

    Task<RepresentativeOperationResult> CreateRepresentativeAsync(
        Guid requesterUserId,
        Guid institutionId,
        CreateRepresentativeCommand command,
        CancellationToken cancellationToken = default);

    Task<InstitutionInviteCreateResult> CreateInviteAsync(
        Guid requesterUserId,
        Guid institutionId,
        CreateInstitutionInviteCommand command,
        CancellationToken cancellationToken = default);

    Task<InstitutionInviteClaimResult> ClaimInviteAsync(
        Guid requesterUserId,
        string token,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default);
}
