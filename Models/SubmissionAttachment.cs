namespace CMIForge.Models;

public class SubmissionAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FormSubmissionId { get; set; }

    public FormSubmission? FormSubmission { get; set; }

    public string AttachmentType { get; set; } = SubmissionAttachmentTypes.File;

    public string DisplayName { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string FileExtension { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string BlobContainer { get; set; } = string.Empty;

    public string BlobName { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public Guid? UploadedByUserId { get; set; }

    public CMIForgeUser? UploadedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class SubmissionAttachmentTypes
{
    public const string File = "File";
    public const string Link = "Link";
}
