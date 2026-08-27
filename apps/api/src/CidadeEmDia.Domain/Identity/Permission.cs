using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Identity;

public sealed class Permission : BaseEntity
{
    private Permission() { }

    public Permission(string key, string description)
    {
        Key = key.Trim().ToLowerInvariant();
        Description = description.Trim();
    }

    public string Key { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ICollection<RolePermission> Roles { get; private set; } = new List<RolePermission>();
}
