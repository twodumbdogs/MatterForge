namespace CMIForge.Models;

public class TimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int TimeEntryNumber { get; set; }

    public Guid UserId { get; set; }

    public CMIForgeUser? User { get; set; }

    public Guid ClientId { get; set; }

    public Client? Client { get; set; }

    public Guid MatterId { get; set; }

    public Matter? Matter { get; set; }

    public Guid? TimePhaseId { get; set; }

    public TimePhase? TimePhase { get; set; }

    public Guid? TimeTaskId { get; set; }

    public TimeTask? TimeTask { get; set; }

    public DateOnly WorkDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public int Minutes { get; set; }

    public string ClientNarrative { get; set; } = string.Empty;

    public string InternalNotes { get; set; } = string.Empty;

    public bool IsBillable { get; set; } = true;

    public string Status { get; set; } = "Draft";

    public DateTimeOffset? SubmittedAt { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public CMIForgeUser? ApprovedByUser { get; set; }

    public DateTimeOffset? ExportedAt { get; set; }

    public Guid? ExportedByUserId { get; set; }

    public CMIForgeUser? ExportedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsLocked => Status == Services.TimeEntryStatuses.Approved || ExportedAt.HasValue;
}
