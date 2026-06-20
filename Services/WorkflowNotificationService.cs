using CMIForge.Data;
using CMIForge.Models;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public sealed record WorkflowNotificationResult(bool Sent, string EventType, string Message);

public class WorkflowNotificationService(CMIForgeDbContext db, IConfiguration configuration)
{
    public async Task<WorkflowNotificationResult> ProcessAsync(
        SubmissionWorkflowInstance instance,
        FormSubmission submission,
        WorkflowDefinition workflow,
        WorkflowStep step,
        Guid? actorUserId = null)
    {
        var recipients = await ResolveRecipientsAsync(submission, step);
        if (recipients.Count == 0)
        {
            var skipped = $"Notification step '{step.Name}' skipped because no recipients were resolved.";
            AddEvent(instance.Id, submission.Id, WorkflowStatuses.EventNotificationSkipped, skipped, actorUserId);
            return new WorkflowNotificationResult(false, WorkflowStatuses.EventNotificationSkipped, skipped);
        }

        var tenantSettings = await LoadTenantEmailSettingsAsync();
        if (!IsEnabled(tenantSettings))
        {
            var skipped = $"Notification step '{step.Name}' prepared for {recipients.Count} recipient(s). Email notifications are disabled.";
            AddEvent(instance.Id, submission.Id, WorkflowStatuses.EventNotificationSkipped, skipped, actorUserId);
            return new WorkflowNotificationResult(false, WorkflowStatuses.EventNotificationSkipped, skipped);
        }

        var fromEmail = Setting(tenantSettings, "Email.FromEmail", PlatformSetting("Notifications:FromEmail", "Email:FromEmail"));
        var mailboxAddress = Setting(tenantSettings, "Email.MailboxAddress", fromEmail);
        if (string.IsNullOrWhiteSpace(mailboxAddress) || string.IsNullOrWhiteSpace(fromEmail))
        {
            var skipped = $"Notification step '{step.Name}' skipped because the tenant email sender is not configured.";
            AddEvent(instance.Id, submission.Id, WorkflowStatuses.EventNotificationSkipped, skipped, actorUserId);
            return new WorkflowNotificationResult(false, WorkflowStatuses.EventNotificationSkipped, skipped);
        }

        try
        {
            var template = step.NotificationTemplateId.HasValue
                ? await db.WorkflowNotificationTemplates
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == step.NotificationTemplateId.Value && x.IsActive)
                : null;
            var subject = RenderTemplate(
                string.IsNullOrWhiteSpace(template?.Subject ?? step.NotificationSubject)
                    ? $"CMIForge workflow notification: {workflow.Name}"
                    : template?.Subject ?? step.NotificationSubject,
                submission,
                workflow,
                step);
            var body = RenderTemplate(
                string.IsNullOrWhiteSpace(template?.Body ?? step.NotificationBody)
                    ? $"{workflow.Name} reached workflow step '{step.Name}' for submission {RecordNumbers.Submission(submission.SubmissionNumber)}."
                    : template?.Body ?? step.NotificationBody,
                submission,
                workflow,
                step);
            var replyToEmail = Setting(tenantSettings, "Email.ReplyToEmail", fromEmail);

            db.EmailOutboxMessages.Add(new EmailOutboxMessage
            {
                MailboxAddress = mailboxAddress,
                FromEmail = fromEmail,
                FromName = Setting(tenantSettings, "Email.FromName", ProductInfo.Name),
                ReplyToEmail = replyToEmail,
                ToRecipients = string.Join(';', recipients),
                Subject = subject,
                Body = body,
                IsBodyHtml = false,
                FormSubmissionId = submission.Id,
                SubmissionWorkflowInstanceId = instance.Id,
                WorkflowStepId = step.Id
            });

            var queued = $"Notification step '{step.Name}' queued for {recipients.Count} recipient(s).";
            AddEvent(instance.Id, submission.Id, WorkflowStatuses.EventNotificationQueued, queued, actorUserId);
            return new WorkflowNotificationResult(true, WorkflowStatuses.EventNotificationQueued, queued);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            var failed = $"Notification step '{step.Name}' could not be queued: {ex.Message}";
            AddEvent(instance.Id, submission.Id, WorkflowStatuses.EventNotificationFailed, failed, actorUserId);
            return new WorkflowNotificationResult(false, WorkflowStatuses.EventNotificationFailed, failed);
        }
    }

    private async Task<Dictionary<string, string>> LoadTenantEmailSettingsAsync()
    {
        return await db.SystemSettings
            .AsNoTracking()
            .Where(x => (x.Category == "Email" || x.Key.StartsWith("Email.")) &&
                !x.Key.StartsWith("Email.Smtp"))
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<List<string>> ResolveRecipientsAsync(FormSubmission submission, WorkflowStep step)
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tokens = string.IsNullOrWhiteSpace(step.NotificationRecipients)
            ? ["assigned"]
            : step.NotificationRecipients
                .Split([';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            if (WorkflowNotificationRecipientTokens.IsAssigned(token))
            {
                await AddAssignedRecipientsAsync(step, recipients);
                continue;
            }

            if (WorkflowNotificationRecipientTokens.IsSubmitter(token))
            {
                await AddSubmitterRecipientAsync(submission, recipients);
                continue;
            }

            if (WorkflowNotificationRecipientTokens.TryReadUserId(token, out var userId))
            {
                await AddUserRecipientAsync(userId, recipients);
                continue;
            }

            if (WorkflowNotificationRecipientTokens.TryReadContactId(token, out var contactId))
            {
                await AddContactRecipientAsync(contactId, recipients);
                continue;
            }

            if (token.Contains('@', StringComparison.Ordinal))
            {
                recipients.Add(token);
            }
        }

        return recipients.OrderBy(x => x).ToList();
    }

    private async Task AddUserRecipientAsync(Guid userId, HashSet<string> recipients)
    {
        var email = await db.Users
            .Where(x => x.Id == userId && x.IsActive && !x.IsArchived)
            .Select(x => x.Email)
            .FirstOrDefaultAsync();
        AddEmail(email, recipients);
    }

    private async Task AddContactRecipientAsync(Guid contactId, HashSet<string> recipients)
    {
        var email = await db.Contacts
            .Where(x => x.Id == contactId && !x.IsArchived)
            .Select(x => x.Email)
            .FirstOrDefaultAsync();
        AddEmail(email, recipients);
    }

    private async Task AddAssignedRecipientsAsync(WorkflowStep step, HashSet<string> recipients)
    {
        if (step.AssignedUserId.HasValue)
        {
            var email = await db.Users
                .Where(x => x.Id == step.AssignedUserId.Value && x.IsActive && !x.IsArchived)
                .Select(x => x.Email)
                .FirstOrDefaultAsync();
            AddEmail(email, recipients);
        }

        if (step.AssignedTeamId.HasValue)
        {
            var teamEmails = await db.TeamMembers
                .Where(x => x.TeamId == step.AssignedTeamId.Value && x.User != null && x.User.IsActive && !x.User.IsArchived)
                .Select(x => x.User!.Email)
                .ToListAsync();

            foreach (var email in teamEmails)
            {
                AddEmail(email, recipients);
            }
        }
    }

    private async Task AddSubmitterRecipientAsync(FormSubmission submission, HashSet<string> recipients)
    {
        if (submission.SubmitterUserId.HasValue)
        {
            var email = await db.Users
                .Where(x => x.Id == submission.SubmitterUserId.Value && x.IsActive && !x.IsArchived)
                .Select(x => x.Email)
                .FirstOrDefaultAsync();
            AddEmail(email, recipients);
        }
    }

    private static void AddEmail(string? value, HashSet<string> recipients)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@', StringComparison.Ordinal))
        {
            return;
        }

        recipients.Add(value.Trim());
    }

    private static bool IsEnabled(Dictionary<string, string> settings)
    {
        return ParseBool(Setting(settings, "Email.NotificationsEnabled"), false);
    }

    private static string Setting(Dictionary<string, string> settings, string key, string fallback = "")
    {
        return settings.TryGetValue(key, out var value) ? value.Trim() : fallback;
    }

    private string PlatformSetting(string key, string legacyKey)
    {
        return (configuration[key] ?? configuration[legacyKey] ?? string.Empty).Trim();
    }

    private static bool ParseBool(string value, bool fallback)
    {
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string RenderTemplate(string template, FormSubmission submission, WorkflowDefinition workflow, WorkflowStep step)
    {
        return template
            .Replace("{{SubmissionNumber}}", RecordNumbers.Submission(submission.SubmissionNumber), StringComparison.OrdinalIgnoreCase)
            .Replace("{{SubmissionStatus}}", submission.Status, StringComparison.OrdinalIgnoreCase)
            .Replace("{{SubmitterName}}", submission.SubmitterName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{WorkflowName}}", workflow.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{{StepName}}", step.Name, StringComparison.OrdinalIgnoreCase);
    }

    private void AddEvent(Guid instanceId, Guid submissionId, string eventType, string message, Guid? actorUserId)
    {
        db.SubmissionWorkflowEvents.Add(new SubmissionWorkflowEvent
        {
            SubmissionWorkflowInstanceId = instanceId,
            FormSubmissionId = submissionId,
            EventType = eventType,
            Message = message,
            ActorUserId = actorUserId
        });
    }
}

public static class WorkflowNotificationRecipientTokens
{
    public const string Assigned = "assigned";
    public const string Submitter = "submitter";
    private const string UserPrefix = "user:";
    private const string ContactPrefix = "contact:";

    public static string User(Guid id) => $"{UserPrefix}{id:D}";

    public static string Contact(Guid id) => $"{ContactPrefix}{id:D}";

    public static bool IsAssigned(string? token) => token?.Equals(Assigned, StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsSubmitter(string? token) => token?.Equals(Submitter, StringComparison.OrdinalIgnoreCase) == true;

    public static bool TryReadUserId(string? token, out Guid id)
    {
        id = Guid.Empty;
        return token?.StartsWith(UserPrefix, StringComparison.OrdinalIgnoreCase) == true &&
            Guid.TryParse(token[UserPrefix.Length..], out id);
    }

    public static bool TryReadContactId(string? token, out Guid id)
    {
        id = Guid.Empty;
        return token?.StartsWith(ContactPrefix, StringComparison.OrdinalIgnoreCase) == true &&
            Guid.TryParse(token[ContactPrefix.Length..], out id);
    }
}
