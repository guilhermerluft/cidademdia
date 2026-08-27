using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Identity;

public sealed class Role : BaseEntity
{
    private Role() { }

    public Role(string key, string name)
    {
        Key = key.Trim().ToUpperInvariant();
        Name = name.Trim();
    }

    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public ICollection<UserRole> Users { get; private set; } = new List<UserRole>();
    public ICollection<RolePermission> Permissions { get; private set; } = new List<RolePermission>();
}
