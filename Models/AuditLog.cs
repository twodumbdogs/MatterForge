namespace MatterForge.Models;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? ActorUserId { get; set; }

    public MatterForgeUser? ActorUser { get; set; }

    public string ActorDisplayName { get; set; } = string.Empty;

    public string ActorEmail { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string EntityNumber { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string DetailsJson { get; set; } = "{}";

    public string IpAddress { get; set; } = string.Empty;

    public string UserAgent { get; set; } = string.Empty;
}
