using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Identity;
using CMIForge.Models;
using Microsoft.Extensions.Options;

namespace CMIForge.Services;

public class SubmissionAttachmentService(IOptions<SubmissionAttachmentStorageOptions> options)
{
    public const long MaxFileBytes = 25L * 1024 * 1024;

    public const string AllowedFileDescription = "PDF, DOC, DOCX, XLS, or XLSX up to 25 MB.";

    private static readonly Dictionary<string, string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    private readonly SubmissionAttachmentStorageOptions storageOptions = options.Value;

    public string ContainerName => string.IsNullOrWhiteSpace(storageOptions.ContainerName)
        ? "submission-attachments"
        : storageOptions.ContainerName.Trim();

    public static bool IsAllowedExtension(string extension)
    {
        return AllowedContentTypes.ContainsKey(extension);
    }

    public static string ContentTypeForExtension(string extension)
    {
        return AllowedContentTypes.TryGetValue(extension, out var contentType)
            ? contentType
            : "application/octet-stream";
    }

    public static bool TryNormalizeLink(string value, out string normalizedUrl, out string error)
    {
        normalizedUrl = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Enter a hyperlink before adding it.";
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "Use a full http:// or https:// hyperlink.";
            return false;
        }

        normalizedUrl = uri.ToString();
        return true;
    }

    public async Task<SubmissionAttachment> UploadFileAsync(
        FormSubmission submission,
        IFormFile file,
        string displayName,
        Guid? uploadedByUserId)
    {
        ValidateStorageConfigured();

        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Choose a file before uploading.");
        }

        if (file.Length > MaxFileBytes)
        {
            throw new InvalidOperationException("Files must be 25 MB or smaller.");
        }

        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!IsAllowedExtension(extension))
        {
            throw new InvalidOperationException($"Only {AllowedFileDescription}");
        }

        var attachment = new SubmissionAttachment
        {
            FormSubmissionId = submission.Id,
            AttachmentType = SubmissionAttachmentTypes.File,
            DisplayName = CleanDisplayName(displayName, Path.GetFileNameWithoutExtension(originalFileName)),
            OriginalFileName = TrimToMax(originalFileName, 260),
            ContentType = ContentTypeForExtension(extension),
            FileExtension = extension,
            SizeBytes = file.Length,
            BlobContainer = ContainerName,
            BlobName = $"submissions/{submission.SubmissionNumber:D8}/{Guid.NewGuid():N}{extension}",
            UploadedByUserId = uploadedByUserId
        };

        var container = await GetContainerAsync();
        var blob = container.GetBlobClient(attachment.BlobName);
        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = attachment.ContentType,
                ContentDisposition = $"attachment; filename=\"{attachment.OriginalFileName.Replace("\"", string.Empty)}\""
            },
            Metadata = new Dictionary<string, string>
            {
                ["submissionNumber"] = submission.SubmissionNumber.ToString("D8"),
                ["attachmentId"] = attachment.Id.ToString("N")
            }
        });

        return attachment;
    }

    public SubmissionAttachment CreateLink(FormSubmission submission, string url, string displayName, Guid? uploadedByUserId)
    {
        if (!TryNormalizeLink(url, out var normalizedUrl, out var error))
        {
            throw new InvalidOperationException(error);
        }

        if (normalizedUrl.Length > 2000)
        {
            throw new InvalidOperationException("Hyperlinks must be 2,000 characters or shorter.");
        }

        return new SubmissionAttachment
        {
            FormSubmissionId = submission.Id,
            AttachmentType = SubmissionAttachmentTypes.Link,
            DisplayName = CleanDisplayName(displayName, normalizedUrl),
            Url = normalizedUrl,
            ContentType = "text/uri-list",
            UploadedByUserId = uploadedByUserId
        };
    }

    public async Task<Stream> OpenReadAsync(SubmissionAttachment attachment)
    {
        ValidateStorageConfigured();

        if (attachment.AttachmentType != SubmissionAttachmentTypes.File || string.IsNullOrWhiteSpace(attachment.BlobName))
        {
            throw new InvalidOperationException("Only uploaded files can be downloaded.");
        }

        var container = await GetContainerAsync();
        var blob = container.GetBlobClient(attachment.BlobName);
        var response = await blob.DownloadStreamingAsync();
        return response.Value.Content;
    }

    public async Task DeleteFileIfExistsAsync(SubmissionAttachment attachment)
    {
        if (attachment.AttachmentType != SubmissionAttachmentTypes.File ||
            string.IsNullOrWhiteSpace(attachment.BlobName))
        {
            return;
        }

        ValidateStorageConfigured();

        var container = await GetContainerAsync();
        await container.GetBlobClient(attachment.BlobName).DeleteIfExistsAsync();
    }

    private async Task<BlobContainerClient> GetContainerAsync()
    {
        BlobContainerClient container;
        if (UseManagedIdentityStorage())
        {
            var serviceUri = BuildBlobServiceUri();
            var serviceClient = new BlobServiceClient(serviceUri, new DefaultAzureCredential());
            container = serviceClient.GetBlobContainerClient(ContainerName);
        }
        else
        {
            container = new BlobContainerClient(storageOptions.ConnectionString, ContainerName);
        }

        await container.CreateIfNotExistsAsync(PublicAccessType.None);
        return container;
    }

    private void ValidateStorageConfigured()
    {
        if (!UseManagedIdentityStorage() && string.IsNullOrWhiteSpace(storageOptions.ConnectionString))
        {
            throw new InvalidOperationException("Blob storage is not configured. Set SubmissionAttachments:ConnectionString or enable managed identity storage.");
        }

        if (UseManagedIdentityStorage() &&
            string.IsNullOrWhiteSpace(storageOptions.ServiceUri) &&
            string.IsNullOrWhiteSpace(storageOptions.AccountName))
        {
            throw new InvalidOperationException("Managed identity blob storage is not configured. Set SubmissionAttachments:AccountName or SubmissionAttachments:ServiceUri.");
        }
    }

    private bool UseManagedIdentityStorage()
    {
        return storageOptions.UseManagedIdentity;
    }

    private Uri BuildBlobServiceUri()
    {
        if (!string.IsNullOrWhiteSpace(storageOptions.ServiceUri))
        {
            return new Uri(storageOptions.ServiceUri.Trim());
        }

        return new Uri($"https://{storageOptions.AccountName.Trim()}.blob.core.windows.net");
    }

    private static string CleanDisplayName(string value, string fallback)
    {
        var cleaned = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return TrimToMax(cleaned, 240);
    }

    private static string TrimToMax(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
