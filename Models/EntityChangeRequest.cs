namespace CMIForge.Models;

public class EntityChangeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string EntityNumber { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string Status { get; set; } = EntityChangeRequestStatuses.Pending;

    public string Summary { get; set; } = string.Empty;

    public string CurrentValuesJson { get; set; } = "{}";

    public string ProposedValuesJson { get; set; } = "{}";

    public string RequestNotes { get; set; } = string.Empty;

    public string ReviewNotes { get; set; } = string.Empty;

    public Guid? RequestedByUserId { get; set; }

    public CMIForgeUser? RequestedByUser { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public CMIForgeUser? ReviewedByUser { get; set; }

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReviewedAt { get; set; }
}

public static class EntityChangeRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}
