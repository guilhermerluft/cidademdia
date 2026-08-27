using CidadeEmDia.Application.Profiles;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CidadeEmDia.Infrastructure.Identity;

internal sealed class ProfileService(AppDbContext dbContext) : IProfileService
{
    public async Task<PrivateUserProfile?> GetPrivateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.Profile)
            .Include(x => x.Roles)
                .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        return user?.Profile is null ? null : ToPrivate(user, user.Profile);
    }

    public async Task<PublicUserProfile?> GetPublicAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        return profile is null
            ? null
            : new PublicUserProfile(profile.UserId, profile.DisplayName, profile.AvatarMediaId);
    }

    public async Task<ProfileUpdateResult> UpdateAsync(
        Guid userId,
        string displayName,
        string? document,
        string? phone,
        CancellationToken cancellationToken = default)
    {
        displayName = displayName?.Trim() ?? string.Empty;
        if (displayName.Length is < 2 or > 160)
            return ProfileUpdateResult.Failure("invalid_input");

        if (!BrazilianDocumentValidator.TryNormalize(document, out var normalizedDocument))
            return ProfileUpdateResult.Failure("invalid_document");

        if (!TryNormalizePhone(phone, out var normalizedPhone))
            return ProfileUpdateResult.Failure("invalid_phone");

        var profile = await dbContext.UserProfiles
            .Include(x => x.User)
                .ThenInclude(x => x.Roles)
                    .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (profile is null)
            return ProfileUpdateResult.Failure("profile_not_found");

        profile.Update(displayName, normalizedDocument, normalizedPhone);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ProfileUpdateResult.Success(ToPrivate(profile.User, profile));
    }

    private static PrivateUserProfile ToPrivate(User user, UserProfile profile)
    {
        var roles = user.Roles
            .Select(x => x.Role.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return new PrivateUserProfile(
            user.Id,
            user.Email,
            profile.DisplayName,
            profile.Document,
            profile.Phone,
            profile.AvatarMediaId,
            roles);
    }

    private static bool TryNormalizePhone(string? value, out string? normalized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalized = null;
            return true;
        }

        normalized = new string(value.Where(char.IsDigit).ToArray());
        return normalized.Length is >= 10 and <= 15;
    }
}
