namespace MatterForge.Models;

public class TimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int TimeEntryNumber { get; set; }

    public Guid UserId { get; set; }

    public MatterForgeUser? User { get; set; }

    public Guid ClientId { get; set; }

    public Client? Client { get; set; }

    public Guid MatterId { get; set; }

    public Matter? Matter { get; set; }

    public DateOnly WorkDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public int Minutes { get; set; }

    public string Narrative { get; set; } = string.Empty;

    public bool IsBillable { get; set; } = true;

    public string Status { get; set; } = "Draft";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
