namespace MatterForge.Models;

public class TeamRole
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TeamId { get; set; }

    public Team? Team { get; set; }

    public Guid SecurityRoleId { get; set; }

    public SecurityRole? SecurityRole { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
