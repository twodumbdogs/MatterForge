namespace MatterForge.Services;

public class SubmissionAttachmentStorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "submission-attachments";

    public bool UseManagedIdentity { get; set; }

    public string AccountName { get; set; } = string.Empty;

    public string ServiceUri { get; set; } = string.Empty;
}
