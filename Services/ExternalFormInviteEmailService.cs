using CMIForge.Data;
using CMIForge.Models;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public sealed record ExternalFormInviteResendResult(ExternalFormInvite Invite, string Token, string FormName);

public class ExternalFormInviteEmailService(CMIForgeDbContext db)
{
    public async Task<bool> QueueEmailAsync(
        ExternalFormInvite invite,
        string formName,
        string link,
        CancellationToken cancellationToken = default)
    {
        var settings = await db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Category == "Email" || x.Key.StartsWith("Email."))
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase, cancellationToken);

        if (!ParseBool(Setting(settings, "Email.NotificationsEnabled"), false))
        {
            return false;
        }

        var fromEmail = Setting(settings, "Email.FromEmail");
        var mailboxAddress = Setting(settings, "Email.MailboxAddress", fromEmail);
        if (string.IsNullOrWhiteSpace(fromEmail) || string.IsNullOrWhiteSpace(mailboxAddress))
        {
            return false;
        }

        db.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            MailboxAddress = mailboxAddress,
            FromEmail = fromEmail,
            FromName = Setting(settings, "Email.FromName", ProductInfo.Name),
            ReplyToEmail = Setting(settings, "Email.ReplyToEmail", fromEmail),
            ToRecipients = invite.RecipientEmail,
            Subject = $"Please complete {formName}",
            Body = BuildEmailBody(invite, link),
            IsBodyHtml = false,
            ExternalFormInviteId = invite.Id
        });

        invite.EmailQueuedAt = DateTimeOffset.UtcNow;
        invite.UpdatedAt = invite.EmailQueuedAt.Value;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ExternalFormInviteResendResult> CreateReplacementInviteAsync(
        Guid inviteId,
        Guid? senderUserId,
        CancellationToken cancellationToken = default)
    {
        var source = await db.ExternalFormInvites
            .Include(x => x.FormDefinition)
            .FirstOrDefaultAsync(x => x.Id == inviteId, cancellationToken);

        if (source is null)
        {
            throw new InvalidOperationException("That invite could not be found.");
        }

        if (source.Status == ExternalFormInviteStatuses.Completed)
        {
            throw new InvalidOperationException("Completed invite links cannot be resent.");
        }

        if (source.FormDefinition is null || !source.FormDefinition.IsActive)
        {
            throw new InvalidOperationException("The original form is no longer available.");
        }

        var latestVersion = await db.FormVersions
            .Where(x => x.FormDefinitionId == source.FormDefinitionId && x.IsPublished)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestVersion is null)
        {
            throw new InvalidOperationException("This form does not have a published version to resend.");
        }

        var now = DateTimeOffset.UtcNow;
        if (source.Status != ExternalFormInviteStatuses.Completed)
        {
            source.Status = ExternalFormInviteStatuses.Revoked;
            source.UpdatedAt = now;
        }

        var token = ExternalFormInviteTokenService.GenerateToken();
        var replacement = new ExternalFormInvite
        {
            FormDefinitionId = source.FormDefinitionId,
            FormVersionId = latestVersion.Id,
            RecipientContactId = source.RecipientContactId,
            RecipientName = source.RecipientName,
            RecipientEmail = source.RecipientEmail,
            SenderUserId = senderUserId ?? source.SenderUserId,
            LeadPartnerId = source.LeadPartnerId,
            TokenHash = ExternalFormInviteTokenService.HashToken(token),
            Status = ExternalFormInviteStatuses.Open,
            Message = source.Message,
            ExpiresAt = now.AddDays(ExpirationDays(source))
        };

        db.ExternalFormInvites.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);
        return new ExternalFormInviteResendResult(replacement, token, source.FormDefinition.Name);
    }

    private static int ExpirationDays(ExternalFormInvite source)
    {
        var originalWindow = source.ExpiresAt - source.CreatedAt;
        if (originalWindow.TotalDays <= 0)
        {
            return 7;
        }

        return Math.Clamp((int)Math.Ceiling(originalWindow.TotalDays), 1, 30);
    }

    private static string BuildEmailBody(ExternalFormInvite invite, string link)
    {
        var message = string.IsNullOrWhiteSpace(invite.Message)
            ? "Please complete this secure intake form so we can gather the information needed for review."
            : invite.Message;

        return $"""
            Hello {invite.RecipientName},

            {message}

            Secure form link:
            {link}

            This link expires on {invite.ExpiresAt:MMMM d, yyyy 'at' h:mm tt} UTC.

            Thank you,
            {ProductInfo.Name}
            """;
    }

    private static string Setting(Dictionary<string, string> settings, string key, string fallback = "")
    {
        return settings.TryGetValue(key, out var value) ? value.Trim() : fallback;
    }

    private static bool ParseBool(string value, bool fallback)
    {
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
