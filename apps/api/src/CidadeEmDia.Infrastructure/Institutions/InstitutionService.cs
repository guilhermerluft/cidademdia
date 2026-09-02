using System.Data;
using System.Security.Cryptography;
using System.Text;
using CidadeEmDia.Application.Institutions;
using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Institutions;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CidadeEmDia.Infrastructure.Institutions;

internal sealed class InstitutionService(AppDbContext dbContext) : IInstitutionService
{
    public async Task<InstitutionDirectoryPage> ListAsync(
        string? search = null,
        string? type = null,
        string? stateCode = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = dbContext.Institutions
            .AsNoTracking()
            .Include(x => x.Jurisdictions)
            .Include(x => x.Representatives)
            .Where(x => x.Status == InstitutionStatusKeys.Active);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Name.ToLower().Contains(normalized)
                || x.Slug.Contains(normalized)
                || x.Representatives.Any(r => r.Name.ToLower().Contains(normalized)));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim().ToUpperInvariant();
            query = query.Where(x => x.Type == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(stateCode))
        {
            var normalizedState = stateCode.Trim().ToUpperInvariant();
            query = query.Where(x =>
                x.StateCode == normalizedState
                || x.Jurisdictions.Any(j => j.StateCode == normalizedState));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var institutions = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new InstitutionDirectoryPage(
            institutions.Select(ToItem).ToArray(),
            page,
            pageSize,
            totalItems);
    }

    public async Task<InstitutionOperationResult> GetAsync(
        Guid institutionId,
        CancellationToken cancellationToken = default)
    {
        if (institutionId == Guid.Empty)
            return InstitutionOperationResult.Failure("institution_id_required");

        var institution = await LoadInstitutionAsync(institutionId, cancellationToken);
        return institution is null
            ? InstitutionOperationResult.Failure("institution_not_found")
            : InstitutionOperationResult.Success(ToItem(institution));
    }

    public async Task<InstitutionOperationResult> CreateAsync(
        Guid requesterUserId,
        CreateInstitutionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAdminAsync(requesterUserId, cancellationToken))
            return InstitutionOperationResult.Failure("institution_admin_required");

        Institution institution;
        try
        {
            institution = new Institution(
                command.Name,
                command.Slug,
                command.Type,
                command.ScopeLevel,
                command.Cnpj,
                command.OfficialEmail,
                command.OfficialDomain,
                command.Description,
                command.CityId,
                command.StateCode);

            foreach (var jurisdiction in command.Jurisdictions ?? [])
            {
                institution.Jurisdictions.Add(
                    new InstitutionJurisdiction(
                        institution.Id,
                        jurisdiction.JurisdictionType,
                        jurisdiction.CityId,
                        jurisdiction.StateCode,
                        jurisdiction.CustomAreaLabel));
            }
        }
        catch (DomainException exception)
        {
            return InstitutionOperationResult.Failure(exception.Message, exception.Message);
        }

        dbContext.Institutions.Add(institution);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return InstitutionOperationResult.Failure("institution_persistence_conflict");
        }

        return InstitutionOperationResult.Success(ToItem(institution));
    }

    public async Task<RepresentativeOperationResult> CreateRepresentativeAsync(
        Guid requesterUserId,
        Guid institutionId,
        CreateRepresentativeCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAdminAsync(requesterUserId, cancellationToken))
            return RepresentativeOperationResult.Failure("institution_admin_required");

        var institutionExists = await dbContext.Institutions
            .AnyAsync(x => x.Id == institutionId, cancellationToken);

        if (!institutionExists)
            return RepresentativeOperationResult.Failure("institution_not_found");

        InstitutionRepresentative representative;
        try
        {
            representative = new InstitutionRepresentative(
                institutionId,
                command.Name,
                command.Slug,
                command.PublicRole,
                command.OfficialEmail,
                command.PhotoMediaId,
                command.MandateStart,
                command.MandateEnd,
                command.DisplayOrder);
        }
        catch (DomainException exception)
        {
            return RepresentativeOperationResult.Failure(exception.Message, exception.Message);
        }

        dbContext.InstitutionRepresentatives.Add(representative);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return RepresentativeOperationResult.Failure("representative_persistence_conflict");
        }

        return RepresentativeOperationResult.Success(ToRepresentativeItem(representative));
    }

    public async Task<InstitutionInviteCreateResult> CreateInviteAsync(
        Guid requesterUserId,
        Guid institutionId,
        CreateInstitutionInviteCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAdminAsync(requesterUserId, cancellationToken))
            return InstitutionInviteCreateResult.Failure("institution_admin_required");

        if (command.ExpiresInHours is < 1 or > 168)
            return InstitutionInviteCreateResult.Failure("invite_expiration_invalid");

        var institutionExists = await dbContext.Institutions
            .AnyAsync(x => x.Id == institutionId, cancellationToken);

        if (!institutionExists)
            return InstitutionInviteCreateResult.Failure("institution_not_found");

        InstitutionRepresentative? representative = null;
        if (command.RepresentativeId.HasValue)
        {
            representative = await dbContext.InstitutionRepresentatives
                .FirstOrDefaultAsync(
                    x => x.Id == command.RepresentativeId.Value
                        && x.InstitutionId == institutionId,
                    cancellationToken);

            if (representative is null)
                return InstitutionInviteCreateResult.Failure("representative_not_found");
            if (representative.AccountId.HasValue)
                return InstitutionInviteCreateResult.Failure("representative_already_claimed");

            var pendingInvite = await dbContext.InstitutionInvites
                .AsNoTracking()
                .AnyAsync(
                    x => x.RepresentativeId == representative.Id
                        && x.Status == InstitutionInviteStatusKeys.Pending
                        && x.ExpiresAt > DateTimeOffset.UtcNow,
                    cancellationToken);

            if (pendingInvite)
                return InstitutionInviteCreateResult.Failure("representative_invite_pending");
        }

        var now = DateTimeOffset.UtcNow;
        var rawToken = CreateRawToken();
        var tokenHash = HashToken(rawToken);
        var expectedEmail = string.IsNullOrWhiteSpace(command.ExpectedEmail)
            ? representative?.OfficialEmail
            : command.ExpectedEmail;

        InstitutionInvite invite;
        try
        {
            invite = new InstitutionInvite(
                institutionId,
                representative?.Id,
                expectedEmail,
                tokenHash,
                requesterUserId,
                now.AddHours(command.ExpiresInHours),
                now);
        }
        catch (DomainException exception)
        {
            return InstitutionInviteCreateResult.Failure(exception.Message, exception.Message);
        }

        representative?.MarkInvited();
        dbContext.InstitutionInvites.Add(invite);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return InstitutionInviteCreateResult.Failure("invite_persistence_conflict");
        }

        return InstitutionInviteCreateResult.Success(
            new InstitutionInviteCreatedItem(
                invite.Id,
                invite.InstitutionId,
                invite.RepresentativeId,
                invite.ExpectedEmail,
                rawToken,
                invite.ExpiresAt));
    }

    public async Task<InstitutionInviteClaimResult> ClaimInviteAsync(
        Guid requesterUserId,
        string token,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || string.IsNullOrWhiteSpace(token))
            return InstitutionInviteClaimResult.Failure("invite_claim_invalid");

        var now = at ?? DateTimeOffset.UtcNow;
        var tokenHash = HashToken(token.Trim());

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var invite = await dbContext.InstitutionInvites
            .Include(x => x.Representative)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (invite is null)
            return InstitutionInviteClaimResult.Failure("invite_not_found");

        if (invite.Status == InstitutionInviteStatusKeys.Used)
            return InstitutionInviteClaimResult.Failure("invite_already_used");
        if (invite.Status == InstitutionInviteStatusKeys.Revoked)
            return InstitutionInviteClaimResult.Failure("invite_revoked");
        if (!invite.IsUsable(now))
        {
            invite.MarkExpired(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return InstitutionInviteClaimResult.Failure("invite_expired");
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == requesterUserId, cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
            return InstitutionInviteClaimResult.Failure("invite_user_not_active");

        if (invite.ExpectedEmail is not null
            && !string.Equals(invite.ExpectedEmail, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return InstitutionInviteClaimResult.Failure("invite_email_mismatch");
        }

        try
        {
            var role = invite.RepresentativeId.HasValue
                ? InstitutionMembershipRoleKeys.Representative
                : InstitutionMembershipRoleKeys.Staff;

            var membership = await dbContext.InstitutionMemberships
                .FirstOrDefaultAsync(
                    x => x.InstitutionId == invite.InstitutionId
                        && x.UserId == requesterUserId
                        && x.MembershipRole == role,
                    cancellationToken);

            var wasChanged = false;

            if (invite.Representative is not null)
            {
                if (invite.Representative.AccountId.HasValue
                    && invite.Representative.AccountId != requesterUserId)
                {
                    return InstitutionInviteClaimResult.Failure("representative_already_claimed");
                }

                invite.Representative.Claim(requesterUserId);
                wasChanged = true;
            }

            if (membership is null)
            {
                membership = new InstitutionMembership(
                    invite.InstitutionId,
                    requesterUserId,
                    invite.RepresentativeId,
                    role,
                    now);
                dbContext.InstitutionMemberships.Add(membership);
                wasChanged = true;
            }

            invite.MarkUsed(now);
            wasChanged = true;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return InstitutionInviteClaimResult.Success(
                ToMembershipItem(membership),
                invite.Representative is null
                    ? null
                    : ToRepresentativeItem(invite.Representative),
                wasChanged);
        }
        catch (DomainException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return InstitutionInviteClaimResult.Failure(exception.Message, exception.Message);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return InstitutionInviteClaimResult.Failure("invite_claim_conflict");
        }
    }

    private Task<Institution?> LoadInstitutionAsync(
        Guid institutionId,
        CancellationToken cancellationToken) =>
        dbContext.Institutions
            .AsNoTracking()
            .Include(x => x.Jurisdictions)
            .Include(x => x.Representatives)
            .FirstOrDefaultAsync(x => x.Id == institutionId, cancellationToken);

    private async Task<bool> IsAdminAsync(
        Guid requesterUserId,
        CancellationToken cancellationToken)
    {
        if (requesterUserId == Guid.Empty)
            return false;

        return await dbContext.UserRoles
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == requesterUserId
                    && x.Role.Key == IdentityRoleKeys.Admin,
                cancellationToken);
    }

    private static InstitutionItem ToItem(Institution institution) =>
        new(
            institution.Id,
            institution.Name,
            institution.Slug,
            institution.Type,
            institution.ScopeLevel,
            institution.OfficialEmail,
            institution.OfficialDomain,
            institution.Description,
            institution.LogoMediaId,
            institution.CityId,
            institution.StateCode,
            institution.Status,
            institution.Jurisdictions
                .OrderBy(x => x.JurisdictionType)
                .ThenBy(x => x.StateCode)
                .Select(x => new InstitutionJurisdictionItem(
                    x.Id,
                    x.JurisdictionType,
                    x.CityId,
                    x.StateCode,
                    x.CustomAreaLabel))
                .ToArray(),
            institution.Representatives
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .Select(ToRepresentativeItem)
                .ToArray());

    private static InstitutionRepresentativeItem ToRepresentativeItem(
        InstitutionRepresentative representative) =>
        new(
            representative.Id,
            representative.InstitutionId,
            representative.Name,
            representative.Slug,
            representative.PublicRole,
            representative.OfficialEmail,
            representative.PhotoMediaId,
            representative.MandateStart,
            representative.MandateEnd,
            representative.AccountId,
            representative.ProfileStatus,
            representative.DisplayOrder);

    private static InstitutionMembershipItem ToMembershipItem(
        InstitutionMembership membership) =>
        new(
            membership.Id,
            membership.InstitutionId,
            membership.UserId,
            membership.RepresentativeId,
            membership.MembershipRole,
            membership.Status,
            membership.JoinedAt);

    private static string CreateRawToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string HashToken(string token) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();
}
