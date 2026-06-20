namespace CMIForge.Models;

public class FormSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FormDefinitionId { get; set; }

    public FormDefinition? FormDefinition { get; set; }

    public Guid FormVersionId { get; set; }

    public FormVersion? FormVersion { get; set; }

    public int SubmissionNumber { get; set; }

    public string SubmitterName { get; set; } = string.Empty;

    public Guid? SubmitterUserId { get; set; }

    public CMIForgeUser? SubmitterUser { get; set; }

    public Guid? LeadPartnerId { get; set; }

    public CMIForgeUser? LeadPartner { get; set; }

    public string Status { get; set; } = "Submitted";

    public string DataJson { get; set; } = "{}";

    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? ClientId { get; set; }

    public Client? Client { get; set; }

    public Guid? MatterId { get; set; }

    public Matter? Matter { get; set; }

    public List<SubmissionWorkflowInstance> WorkflowInstances { get; set; } = [];

    public List<SubmissionWorkflowTask> WorkflowTasks { get; set; } = [];

    public List<SubmissionAttachment> Attachments { get; set; } = [];
}
