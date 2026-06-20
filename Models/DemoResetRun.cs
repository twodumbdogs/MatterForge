namespace CMIForge.Models;

public class DemoResetRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Trigger { get; set; } = string.Empty;

    public string Status { get; set; } = DemoResetRunStatuses.Running;

    public int DeletedRows { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Error { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }
}

public static class DemoResetRunStatuses
{
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}
