namespace CMIForge.Models;

public class DashboardAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DashboardKey { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public CMIForgeUser? User { get; set; }

    public Guid? TeamId { get; set; }

    public Team? Team { get; set; }

    public Guid? SecurityRoleId { get; set; }

    public SecurityRole? SecurityRole { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
