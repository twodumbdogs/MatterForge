namespace MatterForge.Models;

public class WorkflowStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkflowDefinitionId { get; set; }

    public WorkflowDefinition? WorkflowDefinition { get; set; }

    public int StepNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public Guid? AssignedUserId { get; set; }

    public MatterForgeUser? AssignedUser { get; set; }

    public Guid? AssignedTeamId { get; set; }

    public Team? AssignedTeam { get; set; }

    public string ApprovalLabel { get; set; } = "Approve";

    public string CompletionSubmissionStatus { get; set; } = "In Review";

    public string OutcomesJson { get; set; } = "[]";

    public string ConditionFieldKey { get; set; } = string.Empty;

    public string ConditionOperator { get; set; } = "Always";

    public string ConditionValue { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<SubmissionWorkflowTask> Tasks { get; set; } = [];
}
