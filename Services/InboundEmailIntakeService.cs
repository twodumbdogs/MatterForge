using System.Text.Json;
using System.Text.RegularExpressions;
using CMIForge.Data;
using CMIForge.Models;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public class InboundEmailIntakeService(
    CMIForgeDbContext db,
    SubmissionAttachmentService attachmentService,
    WorkflowService workflowService,
    ProductPlanService productPlanService,
    AuditLogService auditLogService,
    ILogger<InboundEmailIntakeService> logger)
{
    public async Task<InboundEmailProcessResult> ProcessAsync(InboundEmailEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        if (!InboundEmailSettings.Bool(settings, InboundEmailSettingKeys.Enabled, false))
        {
            return new InboundEmailProcessResult(false, null, null, "Inbound email intake is disabled.");
        }

        var mailboxAddress = InboundEmailSettings.Value(settings, InboundEmailSettingKeys.MailboxAddress);
        var inboundAddress = InboundEmailSettings.Value(settings, InboundEmailSettingKeys.InboundAddress, mailboxAddress);
        if (!string.IsNullOrWhiteSpace(mailboxAddress) &&
            !mailboxAddress.Equals(envelope.MailboxAddress, StringComparison.OrdinalIgnoreCase))
        {
            return new InboundEmailProcessResult(false, null, null, "Message was read from a mailbox that is not configured for this tenant.");
        }

        var existing = await FindExistingAsync(envelope, cancellationToken);
        if (existing is not null)
        {
            return new InboundEmailProcessResult(existing.Status == InboundEmailStatuses.Processed, existing, existing.FormSubmission, "Inbound email was already recorded.");
        }

        var parsed = InboundEmailSubjectParser.Parse(envelope.Subject);
        var inbound = new InboundEmailMessage
        {
            Status = InboundEmailStatuses.Received,
            MailboxAddress = Trim(envelope.MailboxAddress, 254),
            InboundAddress = Trim(inboundAddress, 254),
            GraphMessageId = Trim(envelope.GraphMessageId, 240),
            InternetMessageId = Trim(envelope.InternetMessageId, 500),
            ConversationId = Trim(envelope.ConversationId, 240),
            FromEmail = Trim(envelope.FromEmail, 254),
            FromName = Trim(envelope.FromName, 160),
            Subject = Trim(envelope.Subject, 500),
            BodyPreview = Trim(envelope.BodyPreview, 1000),
            BodyText = Trim(envelope.BodyText, 12000),
            ParsedClientName = Trim(parsed.ClientName, 240),
            ParsedMatterName = Trim(parsed.MatterName, 240),
            ReceivedAt = envelope.ReceivedAt
        };
        db.InboundEmailMessages.Add(inbound);

        var validation = Validate(settings, parsed, envelope.FromEmail);
        if (!validation.Accepted)
        {
            inbound.Status = InboundEmailStatuses.Rejected;
            inbound.ValidationMessage = validation.Message;
            inbound.ProcessedAt = DateTimeOffset.UtcNow;
            inbound.UpdatedAt = inbound.ProcessedAt;
            await db.SaveChangesAsync(cancellationToken);
            await auditLogService.LogExternalAsync(
                inbound.FromName,
                inbound.FromEmail,
                "InboundEmail.Rejected",
                "InboundEmail",
                inbound.Id,
                null,
                validation.Message,
                new { inbound.Subject, inbound.MailboxAddress, inbound.InboundAddress });
            return new InboundEmailProcessResult(false, inbound, null, validation.Message);
        }

        try
        {
            var formKey = InboundEmailSettings.Value(settings, InboundEmailSettingKeys.DefaultFormKey, "new-matter-intake");
            var form = await db.FormDefinitions
                .Include(x => x.Versions)
                .FirstOrDefaultAsync(x => x.Key == formKey && x.IsActive, cancellationToken);
            var version = form?.Versions
                .Where(x => x.IsPublished)
                .OrderByDescending(x => x.VersionNumber)
                .FirstOrDefault();
            if (form is null || version is null)
            {
                throw new InvalidOperationException($"Inbound form '{formKey}' was not found or has no published version.");
            }

            var schema = FormJson.DeserializeSchema(version.SchemaJson);
            var answers = BuildAnswers(schema, parsed, envelope);
            var nextSubmissionNumber = (await db.FormSubmissions.MaxAsync(x => (int?)x.SubmissionNumber, cancellationToken) ?? 0) + 1;
            var submitterName = string.IsNullOrWhiteSpace(envelope.FromName)
                ? envelope.FromEmail
                : envelope.FromName;
            var submission = new FormSubmission
            {
                SubmissionNumber = nextSubmissionNumber,
                FormDefinitionId = form.Id,
                FormVersionId = version.Id,
                SubmitterName = Trim(submitterName, 160),
                Status = SubmissionStatuses.Submitted,
                DataJson = JsonSerializer.Serialize(answers, FormJson.Options),
                SubmittedAt = envelope.ReceivedAt
            };

            db.FormSubmissions.Add(submission);
            inbound.FormSubmission = submission;

            var ocrEnabled = InboundEmailSettings.Bool(settings, InboundEmailSettingKeys.OcrEnabled, false);
            foreach (var file in envelope.Attachments)
            {
                var inboundAttachment = new InboundEmailAttachment
                {
                    InboundEmailMessage = inbound,
                    GraphAttachmentId = Trim(file.ProviderAttachmentId, 240),
                    OriginalFileName = Trim(file.FileName, 260),
                    ContentType = Trim(file.ContentType, 160),
                    SizeBytes = file.Content.LongLength,
                    OcrStatus = ocrEnabled && ShouldQueueOcr(file)
                        ? InboundEmailOcrStatuses.Pending
                        : InboundEmailOcrStatuses.Skipped
                };

                var submissionAttachment = await attachmentService.UploadBytesAsync(
                    submission,
                    file.FileName,
                    file.ContentType,
                    file.Content,
                    Path.GetFileNameWithoutExtension(file.FileName),
                    uploadedByUserId: null);
                inboundAttachment.SubmissionAttachment = submissionAttachment;
                db.InboundEmailAttachments.Add(inboundAttachment);
                db.SubmissionAttachments.Add(submissionAttachment);
            }

            inbound.Status = InboundEmailStatuses.Processed;
            inbound.ProcessedAt = DateTimeOffset.UtcNow;
            inbound.UpdatedAt = inbound.ProcessedAt;
            await db.SaveChangesAsync(cancellationToken);

            await auditLogService.LogExternalAsync(
                inbound.FromName,
                inbound.FromEmail,
                "InboundEmail.Processed",
                "Submission",
                submission.Id,
                RecordNumbers.Submission(submission.SubmissionNumber),
                $"Created submission from inbound email '{inbound.Subject}'.",
                new
                {
                    inbound.Id,
                    inbound.MailboxAddress,
                    inbound.InboundAddress,
                    inbound.ParsedClientName,
                    inbound.ParsedMatterName,
                    AttachmentCount = envelope.Attachments.Count
                });

            if (productPlanService.AllowsFeature(ProductFeatureKeys.Workflow))
            {
                await workflowService.EnsureStartedAsync(submission);
            }

            return new InboundEmailProcessResult(true, inbound, submission, $"Created submission {RecordNumbers.Submission(submission.SubmissionNumber)}.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Inbound email {InboundEmailId} failed during processing.", inbound.Id);
            inbound.Status = InboundEmailStatuses.Failed;
            inbound.AttemptCount++;
            inbound.LastError = Trim(ex.Message, 4000);
            inbound.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return new InboundEmailProcessResult(false, inbound, null, ex.Message);
        }
    }

    private async Task<Dictionary<string, string>> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        return await db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key.StartsWith("InboundEmail."))
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase, cancellationToken);
    }

    private Task<InboundEmailMessage?> FindExistingAsync(InboundEmailEnvelope envelope, CancellationToken cancellationToken)
    {
        return db.InboundEmailMessages
            .Include(x => x.FormSubmission)
            .FirstOrDefaultAsync(x =>
                (!string.IsNullOrWhiteSpace(envelope.GraphMessageId) && x.GraphMessageId == envelope.GraphMessageId) ||
                (!string.IsNullOrWhiteSpace(envelope.InternetMessageId) && x.InternetMessageId == envelope.InternetMessageId),
                cancellationToken);
    }

    private static InboundEmailValidation Validate(Dictionary<string, string> settings, InboundEmailSubjectParts parsed, string fromEmail)
    {
        if (string.IsNullOrWhiteSpace(parsed.ClientName))
        {
            return new InboundEmailValidation(false, "Inbound email subject must include Client: <client name>.");
        }

        if (!AllowedSenderDomain(settings, fromEmail))
        {
            return new InboundEmailValidation(false, $"Inbound sender domain is not allowed for this tenant: {fromEmail}.");
        }

        return new InboundEmailValidation(true, string.Empty);
    }

    private static bool AllowedSenderDomain(Dictionary<string, string> settings, string fromEmail)
    {
        var domains = InboundEmailSettings.Value(settings, InboundEmailSettingKeys.AllowedSenderDomains);
        if (string.IsNullOrWhiteSpace(domains))
        {
            return false;
        }

        var atIndex = fromEmail.LastIndexOf('@');
        if (atIndex < 0 || atIndex == fromEmail.Length - 1)
        {
            return false;
        }

        var senderDomain = fromEmail[(atIndex + 1)..].Trim();
        return domains
            .Split([';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(domain => senderDomain.Equals(domain.TrimStart('@'), StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, object?> BuildAnswers(FormSchema schema, InboundEmailSubjectParts parsed, InboundEmailEnvelope envelope)
    {
        return schema.Fields.ToDictionary<FormField, string, object?>(
            field => field.Key,
            field => ValueFor(field, parsed, envelope),
            StringComparer.OrdinalIgnoreCase);
    }

    private static object? ValueFor(FormField field, InboundEmailSubjectParts parsed, InboundEmailEnvelope envelope)
    {
        var normalizedKey = SubmissionAnswerReader.NormalizeKey(field.Key);
        var normalizedLabel = SubmissionAnswerReader.NormalizeKey(field.Label);
        if (normalizedKey.Contains("clientname", StringComparison.OrdinalIgnoreCase) ||
            normalizedLabel.Contains("clientname", StringComparison.OrdinalIgnoreCase))
        {
            return parsed.ClientName;
        }

        if (normalizedKey.Contains("mattername", StringComparison.OrdinalIgnoreCase) ||
            normalizedLabel.Contains("mattername", StringComparison.OrdinalIgnoreCase))
        {
            return parsed.MatterName;
        }

        if (normalizedKey is "summary" or "mattersummary" ||
            normalizedLabel.Contains("summary", StringComparison.OrdinalIgnoreCase))
        {
            return BuildSummary(parsed, envelope);
        }

        return field.Type == FieldType.Checkbox ? false : string.Empty;
    }

    private static string BuildSummary(InboundEmailSubjectParts parsed, InboundEmailEnvelope envelope)
    {
        var lines = new List<string>
        {
            $"Created from inbound email: {envelope.Subject}",
            $"From: {DisplaySender(envelope)}",
            $"Client: {parsed.ClientName}"
        };
        if (!string.IsNullOrWhiteSpace(parsed.MatterName))
        {
            lines.Add($"Matter: {parsed.MatterName}");
        }

        if (!string.IsNullOrWhiteSpace(envelope.BodyPreview))
        {
            lines.Add(string.Empty);
            lines.Add(envelope.BodyPreview.Trim());
        }

        return Trim(string.Join(Environment.NewLine, lines), 4000);
    }

    private static string DisplaySender(InboundEmailEnvelope envelope)
    {
        return string.IsNullOrWhiteSpace(envelope.FromName)
            ? envelope.FromEmail
            : $"{envelope.FromName} <{envelope.FromEmail}>";
    }

    private static bool ShouldQueueOcr(InboundEmailFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
    }

    private static string Trim(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public static class InboundEmailSubjectParser
{
    private static readonly Regex SegmentRegex = new(
        @"(?<key>client|matter)\s*:\s*(?<value>.*?)(?=(?:;|\||,)?\s*(?:client|matter)\s*:|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static InboundEmailSubjectParts Parse(string subject)
    {
        var client = string.Empty;
        var matter = string.Empty;
        foreach (Match match in SegmentRegex.Matches(subject ?? string.Empty))
        {
            var key = match.Groups["key"].Value;
            var value = CleanValue(match.Groups["value"].Value);
            if (key.Equals("client", StringComparison.OrdinalIgnoreCase))
            {
                client = value;
            }
            else if (key.Equals("matter", StringComparison.OrdinalIgnoreCase))
            {
                matter = value;
            }
        }

        return new InboundEmailSubjectParts(client, matter);
    }

    private static string CleanValue(string value)
    {
        return value.Trim().Trim(';', '|', ',', '-').Trim();
    }
}

public static class InboundEmailSettings
{
    public static string Value(Dictionary<string, string> settings, string key, string fallback = "")
    {
        return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }

    public static bool Bool(Dictionary<string, string> settings, string key, bool fallback)
    {
        var value = Value(settings, key);
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : bool.TryParse(value, out var parsed) && parsed;
    }
}

public static class InboundEmailSettingKeys
{
    public const string Enabled = "InboundEmail.Enabled";
    public const string MailboxAddress = "InboundEmail.MailboxAddress";
    public const string InboundAddress = "InboundEmail.InboundAddress";
    public const string AllowedSenderDomains = "InboundEmail.AllowedSenderDomains";
    public const string DefaultFormKey = "InboundEmail.DefaultFormKey";
    public const string MarkProcessedAsRead = "InboundEmail.MarkProcessedAsRead";
    public const string OcrEnabled = "InboundEmail.OcrEnabled";
}

public sealed record InboundEmailSubjectParts(string ClientName, string MatterName);

public sealed record InboundEmailValidation(bool Accepted, string Message);

public sealed record InboundEmailProcessResult(bool Success, InboundEmailMessage? InboundEmail, FormSubmission? Submission, string Message);

public sealed record InboundEmailEnvelope(
    string MailboxAddress,
    string GraphMessageId,
    string InternetMessageId,
    string ConversationId,
    string FromEmail,
    string FromName,
    string Subject,
    string BodyPreview,
    string BodyText,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<InboundEmailFile> Attachments);

public sealed record InboundEmailFile(
    string ProviderAttachmentId,
    string FileName,
    string ContentType,
    byte[] Content);
