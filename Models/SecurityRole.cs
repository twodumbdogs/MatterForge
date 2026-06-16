namespace MatterForge.Models;

public class SecurityRole
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<UserRole> UserRoles { get; set; } = [];

    public List<TeamRole> TeamRoles { get; set; } = [];

    public List<RolePermission> RolePermissions { get; set; } = [];
}
