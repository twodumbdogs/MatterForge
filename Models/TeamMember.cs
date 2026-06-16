namespace MatterForge.Models;

public class TeamMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TeamId { get; set; }

    public Team? Team { get; set; }

    public Guid UserId { get; set; }

    public MatterForgeUser? User { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
