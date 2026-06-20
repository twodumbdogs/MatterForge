namespace CMIForge.Models;

public class EmailOutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Status { get; set; } = EmailOutboxStatuses.Pending;

    public string Provider { get; set; } = EmailOutboxProviders.MicrosoftGraph;

    public string MailboxAddress { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    public string ReplyToEmail { get; set; } = string.Empty;

    public string ToRecipients { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public bool IsBodyHtml { get; set; }

    public Guid? FormSubmissionId { get; set; }

    public FormSubmission? FormSubmission { get; set; }

    public Guid? SubmissionWorkflowInstanceId { get; set; }

    public SubmissionWorkflowInstance? SubmissionWorkflowInstance { get; set; }

    public Guid? WorkflowStepId { get; set; }

    public WorkflowStep? WorkflowStep { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public string LastError { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class EmailOutboxStatuses
{
    public const string Pending = "Pending";
    public const string Sending = "Sending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";

    public static readonly string[] Active = [Pending, Failed];
}

public static class EmailOutboxProviders
{
    public const string MicrosoftGraph = "MicrosoftGraph";
}
