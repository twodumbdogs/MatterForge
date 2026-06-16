namespace MatterForge.Models;

public class RolePermission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SecurityRoleId { get; set; }

    public SecurityRole? SecurityRole { get; set; }

    public Guid PermissionId { get; set; }

    public Permission? Permission { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
