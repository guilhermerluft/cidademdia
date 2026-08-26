namespace CidadeEmDia.Domain.Identity;

public sealed class UserRole
{
    private UserRole() { }

    public UserRole(Guid userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public User User { get; private set; } = null!;
    public Role Role { get; private set; } = null!;
}
