namespace CMIForge.Models;

public class SubmissionWorkflowTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SubmissionWorkflowInstanceId { get; set; }

    public SubmissionWorkflowInstance? SubmissionWorkflowInstance { get; set; }

    public Guid FormSubmissionId { get; set; }

    public FormSubmission? FormSubmission { get; set; }

    public Guid WorkflowStepId { get; set; }

    public WorkflowStep? WorkflowStep { get; set; }

    public Guid? AssignedUserId { get; set; }

    public CMIForgeUser? AssignedUser { get; set; }

    public Guid? AssignedTeamId { get; set; }

    public Team? AssignedTeam { get; set; }

    public string Status { get; set; } = "Open";

    public string Outcome { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public Guid? CompletedByUserId { get; set; }

    public CMIForgeUser? CompletedByUser { get; set; }
}
