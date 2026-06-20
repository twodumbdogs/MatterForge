namespace CMIForge.Models;

public class WorkflowStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkflowDefinitionId { get; set; }

    public WorkflowDefinition? WorkflowDefinition { get; set; }

    public int StepNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public string StepType { get; set; } = WorkflowStepTypes.Approval;

    public Guid? AssignedUserId { get; set; }

    public CMIForgeUser? AssignedUser { get; set; }

    public Guid? AssignedTeamId { get; set; }

    public Team? AssignedTeam { get; set; }

    public string ApprovalLabel { get; set; } = "Approve";

    public string CompletionSubmissionStatus { get; set; } = "In Review";

    public string OutcomesJson { get; set; } = "[]";

    public string ConditionFieldKey { get; set; } = string.Empty;

    public string ConditionOperator { get; set; } = "Always";

    public string ConditionValue { get; set; } = string.Empty;

    public string NotificationSubject { get; set; } = string.Empty;

    public string NotificationBody { get; set; } = string.Empty;

    public string NotificationRecipients { get; set; } = string.Empty;

    public Guid? NotificationTemplateId { get; set; }

    public WorkflowNotificationTemplate? NotificationTemplate { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<SubmissionWorkflowTask> Tasks { get; set; } = [];
}

public static class WorkflowStepTypes
{
    public const string Approval = "Approval";
    public const string Notification = "Notification";

    public static readonly string[] All = [Approval, Notification];

    public static bool IsValid(string? value)
    {
        return All.Contains(value, StringComparer.OrdinalIgnoreCase);
    }
}
