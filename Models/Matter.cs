namespace CMIForge.Models;

public class Matter
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public int MatterNumber { get; set; }

    public Guid ClientId { get; set; }

    public Client? Client { get; set; }

    public string PracticeArea { get; set; } = string.Empty;

    public string Status { get; set; } = "Open";

    public DateOnly? OpenedDate { get; set; }

    public Guid? ResponsibleUserId { get; set; }

    public CMIForgeUser? ResponsibleUser { get; set; }

    public Guid? LeadPartnerId { get; set; }

    public CMIForgeUser? LeadPartner { get; set; }

    public bool RequiresTimeApproval { get; set; }

    public int? TimeIncrementMinutes { get; set; }

    public Guid? TimeCodeSetId { get; set; }

    public TimeCodeSet? TimeCodeSet { get; set; }

    public string Notes { get; set; } = string.Empty;

    public bool IsArchived { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<MatterParty> Parties { get; set; } = [];

    public List<MatterContact> Contacts { get; set; } = [];

    public List<TimeEntry> TimeEntries { get; set; } = [];
}
