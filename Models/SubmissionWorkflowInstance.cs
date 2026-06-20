namespace CMIForge.Models;

public class SubmissionWorkflowInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FormSubmissionId { get; set; }

    public FormSubmission? FormSubmission { get; set; }

    public Guid WorkflowDefinitionId { get; set; }

    public WorkflowDefinition? WorkflowDefinition { get; set; }

    public string Status { get; set; } = "Active";

    public int CurrentStepNumber { get; set; } = 1;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public List<SubmissionWorkflowTask> Tasks { get; set; } = [];

    public List<SubmissionWorkflowEvent> Events { get; set; } = [];
}
