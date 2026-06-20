namespace CMIForge.Models;

public class UserRole
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public CMIForgeUser? User { get; set; }

    public Guid SecurityRoleId { get; set; }

    public SecurityRole? SecurityRole { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
