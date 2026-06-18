namespace MatterForge.Services;

public static class WorkflowStatuses
{
    public const string Active = "Active";
    public const string Completed = "Completed";
    public const string Returned = "Returned";

    public const string TaskOpen = "Open";
    public const string TaskCompleted = "Completed";
    public const string TaskReturned = "Returned";

    public const string EventStarted = "Started";
    public const string EventStepCompleted = "Step Completed";
    public const string EventReturned = "Returned";
    public const string EventCompleted = "Completed";
    public const string EventNotificationSkipped = "Notification Skipped";
    public const string EventNotificationSent = "Notification Sent";
    public const string EventNotificationFailed = "Notification Failed";
}
