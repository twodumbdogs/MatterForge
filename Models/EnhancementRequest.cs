namespace CMIForge.Models;

public class EnhancementRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Area { get; set; } = EnhancementRequestAreas.General;

    public string Priority { get; set; } = EnhancementRequestPriorities.NiceToHave;

    public string Description { get; set; } = string.Empty;

    public string BusinessValue { get; set; } = string.Empty;

    public string Status { get; set; } = EnhancementRequestStatuses.New;

    public string InternalNotes { get; set; } = string.Empty;

    public Guid? SubmittedByUserId { get; set; }

    public CMIForgeUser? SubmittedByUser { get; set; }

    public string SubmittedByDisplayName { get; set; } = string.Empty;

    public string SubmittedByEmail { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public CMIForgeUser? UpdatedByUser { get; set; }
}

public static class EnhancementRequestAreas
{
    public const string General = "General";
    public const string IntakeForms = "Intake/forms";
    public const string Conflicts = "Conflicts";
    public const string Entities = "Entities";
    public const string Workflow = "Workflow";
    public const string Reporting = "Reporting";
    public const string Notifications = "Notifications";
}

public static class EnhancementRequestPriorities
{
    public const string NiceToHave = "Nice to have";
    public const string Useful = "Useful";
    public const string Important = "Important";
    public const string Urgent = "Urgent";
}

public static class EnhancementRequestStatuses
{
    public const string New = "New";
    public const string Reviewing = "Reviewing";
    public const string Planned = "Planned";
    public const string Shipped = "Shipped";
    public const string Closed = "Closed";
}
