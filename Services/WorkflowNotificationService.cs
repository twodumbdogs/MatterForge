using System.Net;
using System.Net.Mail;
using MatterForge.Data;
using MatterForge.Models;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Services;

public sealed record WorkflowNotificationResult(bool Sent, string EventType, string Message);

public class WorkflowNotificationService(MatterForgeDbContext db)
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

        var settings = await LoadSettingsAsync();
        if (!IsEnabled(settings))
        {
            var skipped = $"Notification step '{step.Name}' prepared for {recipients.Count} recipient(s). Email notifications are disabled.";
            AddEvent(instance.Id, submission.Id, WorkflowStatuses.EventNotificationSkipped, skipped, actorUserId);
            return new WorkflowNotificationResult(false, WorkflowStatuses.EventNotificationSkipped, skipped);
        }

        var host = Setting(settings, "Email.SmtpHost");
        var fromEmail = Setting(settings, "Email.FromEmail");
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
        {
            var skipped = $"Notification step '{step.Name}' skipped because SMTP host/from settings are incomplete.";
            AddEvent(instance.Id, submission.Id, WorkflowStatuses.EventNotificationSkipped, skipped, actorUserId);
            return new WorkflowNotificationResult(false, WorkflowStatuses.EventNotificationSkipped, skipped);
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, Setting(settings, "Email.FromName", ProductInfo.Name)),
                Subject = RenderTemplate(
                    string.IsNullOrWhiteSpace(step.NotificationSubject)
                        ? $"CMIForge workflow notification: {workflow.Name}"
                        : step.NotificationSubject,
                    submission,
                    workflow,
                    step),
                Body = RenderTemplate(
                    string.IsNullOrWhiteSpace(step.NotificationBody)
                        ? $"{workflow.Name} reached workflow step '{step.Name}' for submission {RecordNumbers.Submission(submission.SubmissionNumber)}."
                        : step.NotificationBody,
                    submission,
                    workflow,
                    step),
                IsBodyHtml = false
            };

            foreach (var recipient in recipients)
            {
                message.To.Add(recipient);
            }

            using var client = new SmtpClient(host, ParseInt(Setting(settings, "Email.SmtpPort"), 587))
            {
                EnableSsl = ParseBool(Setting(settings, "Email.SmtpUseSsl"), true)
            };

            var username = Setting(settings, "Email.SmtpUsername");
            if (!string.IsNullOrWhiteSpace(username))
            {
                client.Credentials = new NetworkCredential(username, string.Empty);
            }

            await client.SendMailAsync(message);

            var sent = $"Notification step '{step.Name}' sent to {recipients.Count} recipient(s).";
            AddEvent(instance.Id, submission.Id, WorkflowStatuses.EventNotificationSent, sent, actorUserId);
            return new WorkflowNotificationResult(true, WorkflowStatuses.EventNotificationSent, sent);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            var failed = $"Notification step '{step.Name}' could not send: {ex.Message}";
            AddEvent(instance.Id, submission.Id, WorkflowStatuses.EventNotificationFailed, failed, actorUserId);
            return new WorkflowNotificationResult(false, WorkflowStatuses.EventNotificationFailed, failed);
        }
    }

    private async Task<Dictionary<string, string>> LoadSettingsAsync()
    {
        return await db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Category == "Email" || x.Key.StartsWith("Email."))
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
            if (token.Equals("assigned", StringComparison.OrdinalIgnoreCase))
            {
                await AddAssignedRecipientsAsync(step, recipients);
                continue;
            }

            if (token.Equals("submitter", StringComparison.OrdinalIgnoreCase))
            {
                await AddSubmitterRecipientAsync(submission, recipients);
                continue;
            }

            if (token.Contains('@', StringComparison.Ordinal))
            {
                recipients.Add(token);
            }
        }

        return recipients.OrderBy(x => x).ToList();
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

    private static bool ParseBool(string value, bool fallback)
    {
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
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
