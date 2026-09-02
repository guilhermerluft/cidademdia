using System.Net.Mail;
using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Institutions;

public static class InstitutionTypeKeys
{
    public const string CityHall = "CITY_HALL";
    public const string CityCouncil = "CITY_COUNCIL";
    public const string Assembly = "ASSEMBLY";
    public const string PublicAgency = "PUBLIC_AGENCY";
    public const string PublicService = "PUBLIC_SERVICE";
    public const string Other = "OTHER";

    public static bool IsSupported(string? value) =>
        value is CityHall or CityCouncil or Assembly or PublicAgency or PublicService or Other;
}

public static class InstitutionScopeLevelKeys
{
    public const string Municipal = "MUNICIPAL";
    public const string State = "STATE";
    public const string Federal = "FEDERAL";
    public const string Regional = "REGIONAL";
    public const string Other = "OTHER";

    public static bool IsSupported(string? value) =>
        value is Municipal or State or Federal or Regional or Other;
}

public static class InstitutionStatusKeys
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
}

public static class InstitutionJurisdictionTypeKeys
{
    public const string City = "CITY";
    public const string State = "STATE";
    public const string CustomArea = "CUSTOM_AREA";

    public static bool IsSupported(string? value) =>
        value is City or State or CustomArea;
}

public static class RepresentativeProfileStatusKeys
{
    public const string NotRegistered = "NOT_REGISTERED";
    public const string Invited = "INVITED";
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
}

public static class InstitutionMembershipRoleKeys
{
    public const string Representative = "REPRESENTATIVE";
    public const string Staff = "STAFF";
    public const string InstitutionAdmin = "INSTITUTION_ADMIN";
    public const string Operator = "OPERATOR";

    public static bool IsSupported(string? value) =>
        value is Representative or Staff or InstitutionAdmin or Operator;
}

public static class InstitutionMembershipStatusKeys
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
}

public static class InstitutionInviteStatusKeys
{
    public const string Pending = "PENDING";
    public const string Used = "USED";
    public const string Revoked = "REVOKED";
    public const string Expired = "EXPIRED";
}

public sealed class Institution : BaseEntity
{
    private Institution() { }

    public Institution(
        string name,
        string slug,
        string type,
        string scopeLevel,
        string? cnpj,
        string? officialEmail,
        string? officialDomain,
        string? description,
        Guid? cityId,
        string? stateCode)
    {
        Name = RequireText(name, 180, "institution_name_required", "institution_name_too_long");
        Slug = NormalizeSlug(slug, "institution_slug_invalid");

        var normalizedType = NormalizeKey(type);
        if (!InstitutionTypeKeys.IsSupported(normalizedType))
            throw new DomainException("institution_type_not_supported");

        var normalizedScopeLevel = NormalizeKey(scopeLevel);
        if (!InstitutionScopeLevelKeys.IsSupported(normalizedScopeLevel))
            throw new DomainException("institution_scope_not_supported");

        Type = normalizedType;
        ScopeLevel = normalizedScopeLevel;
        Cnpj = NormalizeCnpj(cnpj);
        OfficialEmail = NormalizeOptionalEmail(officialEmail, "institution_email_invalid");
        OfficialDomain = NormalizeDomain(officialDomain);
        Description = NormalizeOptional(description, 5000, "institution_description_too_long");
        CityId = cityId;
        StateCode = NormalizeStateCode(stateCode);
        Status = InstitutionStatusKeys.Active;
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public string ScopeLevel { get; private set; } = string.Empty;
    public string? Cnpj { get; private set; }
    public string? OfficialEmail { get; private set; }
    public string? OfficialDomain { get; private set; }
    public string? Description { get; private set; }
    public Guid? LogoMediaId { get; private set; }
    public Guid? CityId { get; private set; }
    public string? StateCode { get; private set; }
    public string Status { get; private set; } = InstitutionStatusKeys.Active;

    public ICollection<InstitutionJurisdiction> Jurisdictions { get; private set; } = new List<InstitutionJurisdiction>();
    public ICollection<InstitutionRepresentative> Representatives { get; private set; } = new List<InstitutionRepresentative>();
    public ICollection<InstitutionMembership> Memberships { get; private set; } = new List<InstitutionMembership>();
    public ICollection<InstitutionInvite> Invites { get; private set; } = new List<InstitutionInvite>();

    public void SetLogoMedia(Guid? mediaId)
    {
        LogoMediaId = mediaId;
        Touch();
    }

    public void Deactivate()
    {
        Status = InstitutionStatusKeys.Inactive;
        Touch();
    }

    public void Activate()
    {
        Status = InstitutionStatusKeys.Active;
        Touch();
    }

    internal static string NormalizeKey(string? value) =>
        value?.Trim().ToUpperInvariant() ?? string.Empty;

    internal static string RequireText(
        string? value,
        int maxLength,
        string requiredCode,
        string tooLongCode)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new DomainException(requiredCode);
        if (normalized.Length > maxLength)
            throw new DomainException(tooLongCode);
        return normalized;
    }

    internal static string? NormalizeOptional(
        string? value,
        int maxLength,
        string tooLongCode)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        if (normalized.Length > maxLength)
            throw new DomainException(tooLongCode);
        return normalized;
    }

    internal static string NormalizeSlug(string? value, string errorCode)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length > 180
            || normalized[0] == '-'
            || normalized[^1] == '-'
            || normalized.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-')))
        {
            throw new DomainException(errorCode);
        }

        return normalized;
    }

    internal static string? NormalizeOptionalEmail(string? value, string errorCode)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        if (normalized.Length > 320 || !MailAddress.TryCreate(normalized, out _))
            throw new DomainException(errorCode);
        return normalized;
    }

    internal static string? NormalizeStateCode(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        if (normalized.Length != 2 || normalized.Any(c => !char.IsAsciiLetter(c)))
            throw new DomainException("institution_state_code_invalid");
        return normalized;
    }

    private static string? NormalizeCnpj(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length != 14)
            throw new DomainException("institution_cnpj_invalid");
        return digits;
    }

    private static string? NormalizeDomain(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        if (normalized.Length > 255
            || normalized.Contains("//", StringComparison.Ordinal)
            || normalized.Contains('/', StringComparison.Ordinal)
            || !normalized.Contains('.', StringComparison.Ordinal))
        {
            throw new DomainException("institution_domain_invalid");
        }
        return normalized;
    }
}

public sealed class InstitutionJurisdiction : BaseEntity
{
    private InstitutionJurisdiction() { }

    public InstitutionJurisdiction(
        Guid institutionId,
        string jurisdictionType,
        Guid? cityId,
        string? stateCode,
        string? customAreaLabel)
    {
        if (institutionId == Guid.Empty)
            throw new DomainException("institution_id_required");

        var normalizedType = Institution.NormalizeKey(jurisdictionType);
        if (!InstitutionJurisdictionTypeKeys.IsSupported(normalizedType))
            throw new DomainException("institution_jurisdiction_type_not_supported");

        var normalizedState = Institution.NormalizeStateCode(stateCode);
        var normalizedCustomArea = Institution.NormalizeOptional(
            customAreaLabel,
            180,
            "institution_custom_area_too_long");

        if (normalizedType == InstitutionJurisdictionTypeKeys.City && (!cityId.HasValue || cityId == Guid.Empty))
            throw new DomainException("institution_jurisdiction_city_required");
        if (normalizedType == InstitutionJurisdictionTypeKeys.State && normalizedState is null)
            throw new DomainException("institution_jurisdiction_state_required");
        if (normalizedType == InstitutionJurisdictionTypeKeys.CustomArea && normalizedCustomArea is null)
            throw new DomainException("institution_jurisdiction_custom_area_required");

        InstitutionId = institutionId;
        JurisdictionType = normalizedType;
        CityId = cityId;
        StateCode = normalizedState;
        CustomAreaLabel = normalizedCustomArea;
    }

    public Guid InstitutionId { get; private set; }
    public string JurisdictionType { get; private set; } = string.Empty;
    public Guid? CityId { get; private set; }
    public string? StateCode { get; private set; }
    public string? CustomAreaLabel { get; private set; }

    public Institution Institution { get; private set; } = null!;
}

public sealed class InstitutionRepresentative : BaseEntity
{
    private InstitutionRepresentative() { }

    public InstitutionRepresentative(
        Guid institutionId,
        string name,
        string slug,
        string publicRole,
        string? officialEmail,
        Guid? photoMediaId,
        DateOnly? mandateStart,
        DateOnly? mandateEnd,
        int displayOrder)
    {
        if (institutionId == Guid.Empty)
            throw new DomainException("institution_id_required");
        if (displayOrder < 0)
            throw new DomainException("representative_display_order_invalid");
        if (mandateStart.HasValue && mandateEnd.HasValue && mandateEnd < mandateStart)
            throw new DomainException("representative_mandate_invalid");

        InstitutionId = institutionId;
        Name = Institution.RequireText(name, 180, "representative_name_required", "representative_name_too_long");
        Slug = Institution.NormalizeSlug(slug, "representative_slug_invalid");
        PublicRole = Institution.RequireText(publicRole, 120, "representative_role_required", "representative_role_too_long");
        OfficialEmail = Institution.NormalizeOptionalEmail(officialEmail, "representative_email_invalid");
        PhotoMediaId = photoMediaId;
        MandateStart = mandateStart;
        MandateEnd = mandateEnd;
        DisplayOrder = displayOrder;
        ProfileStatus = RepresentativeProfileStatusKeys.NotRegistered;
    }

    public Guid InstitutionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string PublicRole { get; private set; } = string.Empty;
    public string? OfficialEmail { get; private set; }
    public Guid? PhotoMediaId { get; private set; }
    public DateOnly? MandateStart { get; private set; }
    public DateOnly? MandateEnd { get; private set; }
    public Guid? AccountId { get; private set; }
    public string ProfileStatus { get; private set; } = RepresentativeProfileStatusKeys.NotRegistered;
    public int DisplayOrder { get; private set; }

    public Institution Institution { get; private set; } = null!;

    public void MarkInvited()
    {
        if (AccountId.HasValue)
            return;
        ProfileStatus = RepresentativeProfileStatusKeys.Invited;
        Touch();
    }

    public void Claim(Guid accountId)
    {
        if (accountId == Guid.Empty)
            throw new DomainException("representative_account_required");
        if (AccountId.HasValue && AccountId.Value != accountId)
            throw new DomainException("representative_already_claimed");

        AccountId = accountId;
        ProfileStatus = RepresentativeProfileStatusKeys.Active;
        Touch();
    }

    public void Deactivate()
    {
        ProfileStatus = RepresentativeProfileStatusKeys.Inactive;
        Touch();
    }
}

public sealed class InstitutionMembership : BaseEntity
{
    private InstitutionMembership() { }

    public InstitutionMembership(
        Guid institutionId,
        Guid userId,
        Guid? representativeId,
        string membershipRole,
        DateTimeOffset joinedAt)
    {
        if (institutionId == Guid.Empty)
            throw new DomainException("institution_id_required");
        if (userId == Guid.Empty)
            throw new DomainException("membership_user_required");

        var normalizedRole = Institution.NormalizeKey(membershipRole);
        if (!InstitutionMembershipRoleKeys.IsSupported(normalizedRole))
            throw new DomainException("membership_role_not_supported");
        if (normalizedRole == InstitutionMembershipRoleKeys.Representative && !representativeId.HasValue)
            throw new DomainException("membership_representative_required");

        InstitutionId = institutionId;
        UserId = userId;
        RepresentativeId = representativeId;
        MembershipRole = normalizedRole;
        Status = InstitutionMembershipStatusKeys.Active;
        JoinedAt = joinedAt;
    }

    public Guid InstitutionId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? RepresentativeId { get; private set; }
    public string MembershipRole { get; private set; } = string.Empty;
    public string Status { get; private set; } = InstitutionMembershipStatusKeys.Active;
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }

    public Institution Institution { get; private set; } = null!;
    public InstitutionRepresentative? Representative { get; private set; }

    public void End(DateTimeOffset endedAt)
    {
        if (Status == InstitutionMembershipStatusKeys.Inactive)
            return;
        Status = InstitutionMembershipStatusKeys.Inactive;
        EndedAt = endedAt;
        Touch();
    }
}

public sealed class InstitutionInvite : BaseEntity
{
    private InstitutionInvite() { }

    public InstitutionInvite(
        Guid institutionId,
        Guid? representativeId,
        string? expectedEmail,
        string tokenHash,
        Guid createdByUserId,
        DateTimeOffset expiresAt,
        DateTimeOffset now)
    {
        if (institutionId == Guid.Empty)
            throw new DomainException("institution_id_required");
        if (createdByUserId == Guid.Empty)
            throw new DomainException("invite_creator_required");
        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Trim().Length != 64)
            throw new DomainException("invite_token_hash_invalid");
        if (expiresAt <= now)
            throw new DomainException("invite_expiration_invalid");

        InstitutionId = institutionId;
        RepresentativeId = representativeId;
        ExpectedEmail = Institution.NormalizeOptionalEmail(expectedEmail, "invite_expected_email_invalid");
        TokenHash = tokenHash.Trim().ToLowerInvariant();
        CreatedByUserId = createdByUserId;
        ExpiresAt = expiresAt;
        Status = InstitutionInviteStatusKeys.Pending;
    }

    public Guid InstitutionId { get; private set; }
    public Guid? RepresentativeId { get; private set; }
    public string? ExpectedEmail { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string Status { get; private set; } = InstitutionInviteStatusKeys.Pending;

    public Institution Institution { get; private set; } = null!;
    public InstitutionRepresentative? Representative { get; private set; }

    public bool IsUsable(DateTimeOffset now) =>
        Status == InstitutionInviteStatusKeys.Pending && now < ExpiresAt;

    public void MarkUsed(DateTimeOffset usedAt)
    {
        if (!IsUsable(usedAt))
            throw new DomainException("invite_not_usable");
        Status = InstitutionInviteStatusKeys.Used;
        UsedAt = usedAt;
        Touch();
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        if (Status != InstitutionInviteStatusKeys.Pending)
            return;
        Status = InstitutionInviteStatusKeys.Revoked;
        RevokedAt = revokedAt;
        Touch();
    }

    public void MarkExpired(DateTimeOffset now)
    {
        if (Status == InstitutionInviteStatusKeys.Pending && now >= ExpiresAt)
        {
            Status = InstitutionInviteStatusKeys.Expired;
            Touch();
        }
    }
}
