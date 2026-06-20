namespace CMIForge.Models;

public class TimeTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TimeCodeSetId { get; set; }

    public TimeCodeSet? TimeCodeSet { get; set; }

    public Guid? TimePhaseId { get; set; }

    public TimePhase? TimePhase { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public List<TimeEntry> TimeEntries { get; set; } = [];
}
