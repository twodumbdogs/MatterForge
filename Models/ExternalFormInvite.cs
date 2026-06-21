namespace CMIForge.Models;

public class ExternalFormInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FormDefinitionId { get; set; }

    public FormDefinition? FormDefinition { get; set; }

    public Guid FormVersionId { get; set; }

    public FormVersion? FormVersion { get; set; }

    public Guid? RecipientContactId { get; set; }

    public Contact? RecipientContact { get; set; }

    public string RecipientName { get; set; } = string.Empty;

    public string RecipientEmail { get; set; } = string.Empty;

    public Guid? SenderUserId { get; set; }

    public CMIForgeUser? SenderUser { get; set; }

    public Guid? LeadPartnerId { get; set; }

    public CMIForgeUser? LeadPartner { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public string Status { get; set; } = ExternalFormInviteStatuses.Open;

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? OpenedAt { get; set; }

    public DateTimeOffset? EmailQueuedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public Guid? FormSubmissionId { get; set; }

    public FormSubmission? FormSubmission { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class ExternalFormInviteStatuses
{
    public const string Open = "Open";
    public const string Completed = "Completed";
    public const string Revoked = "Revoked";
    public const string Expired = "Expired";
}
