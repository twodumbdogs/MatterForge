namespace CMIForge.Models;

public class SubmissionWorkflowEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SubmissionWorkflowInstanceId { get; set; }

    public SubmissionWorkflowInstance? SubmissionWorkflowInstance { get; set; }

    public Guid FormSubmissionId { get; set; }

    public FormSubmission? FormSubmission { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Guid? ActorUserId { get; set; }

    public CMIForgeUser? ActorUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
