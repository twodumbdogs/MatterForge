namespace CMIForge.Models;

public class InboundEmailMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Status { get; set; } = InboundEmailStatuses.Received;

    public string Provider { get; set; } = InboundEmailProviders.MicrosoftGraph;

    public string MailboxAddress { get; set; } = string.Empty;

    public string InboundAddress { get; set; } = string.Empty;

    public string GraphMessageId { get; set; } = string.Empty;

    public string InternetMessageId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string BodyPreview { get; set; } = string.Empty;

    public string BodyText { get; set; } = string.Empty;

    public string ParsedClientName { get; set; } = string.Empty;

    public string ParsedMatterName { get; set; } = string.Empty;

    public string ValidationMessage { get; set; } = string.Empty;

    public int AttemptCount { get; set; }

    public string LastError { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? FormSubmissionId { get; set; }

    public FormSubmission? FormSubmission { get; set; }

    public List<InboundEmailAttachment> Attachments { get; set; } = [];
}

public class InboundEmailAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InboundEmailMessageId { get; set; }

    public InboundEmailMessage? InboundEmailMessage { get; set; }

    public Guid? SubmissionAttachmentId { get; set; }

    public SubmissionAttachment? SubmissionAttachment { get; set; }

    public string GraphAttachmentId { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string OcrStatus { get; set; } = InboundEmailOcrStatuses.Pending;

    public string OcrText { get; set; } = string.Empty;

    public string OcrError { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}

public static class InboundEmailStatuses
{
    public const string Received = "Received";
    public const string Rejected = "Rejected";
    public const string Processed = "Processed";
    public const string Failed = "Failed";
}

public static class InboundEmailOcrStatuses
{
    public const string Pending = "Pending";
    public const string Skipped = "Skipped";
    public const string Extracted = "Extracted";
    public const string Failed = "Failed";
}

public static class InboundEmailProviders
{
    public const string MicrosoftGraph = "MicrosoftGraph";
    public const string Manual = "Manual";
}
