namespace MatterForge.Models;

public class FormVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FormDefinitionId { get; set; }

    public FormDefinition? FormDefinition { get; set; }

    public int VersionNumber { get; set; }

    public string SchemaJson { get; set; } = "{}";

    public Guid? WorkflowDefinitionId { get; set; }

    public WorkflowDefinition? WorkflowDefinition { get; set; }

    public bool IsPublished { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? PublishedAt { get; set; }

    public List<FormSubmission> Submissions { get; set; } = [];
}
