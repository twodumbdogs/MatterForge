namespace CMIForge.Models;

public class EntityNote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string Body { get; set; } = string.Empty;

    public Guid? CreatedByUserId { get; set; }

    public CMIForgeUser? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
