using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Identity;

public sealed class UserProfile : BaseEntity
{
    private UserProfile() { }

    public UserProfile(Guid userId, string displayName)
    {
        UserId = userId;
        DisplayName = displayName.Trim();
    }

    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? Document { get; private set; }
    public string? Phone { get; private set; }
    public Guid? AvatarMediaId { get; private set; }

    public User User { get; private set; } = null!;

    public void Update(string displayName, string? document, string? phone)
    {
        DisplayName = displayName.Trim();
        Document = string.IsNullOrWhiteSpace(document) ? null : document.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Touch();
    }

    public void SetAvatar(Guid? avatarMediaId)
    {
        AvatarMediaId = avatarMediaId;
        Touch();
    }
}
