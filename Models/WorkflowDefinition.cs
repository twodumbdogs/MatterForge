namespace CMIForge.Models;

public class WorkflowDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid? FormDefinitionId { get; set; }

    public FormDefinition? FormDefinition { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<WorkflowStep> Steps { get; set; } = [];

    public List<SubmissionWorkflowInstance> Instances { get; set; } = [];
}
