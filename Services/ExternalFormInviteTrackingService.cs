using CMIForge.Data;
using CMIForge.Models;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public sealed record ExternalFormInviteTrackingRow(
    Guid InviteId,
    Guid FormDefinitionId,
    string FormName,
    string RecipientName,
    string RecipientEmail,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string InviteStatus,
    DateTimeOffset? EmailQueuedAt,
    string EmailStatus,
    DateTimeOffset? EmailSentAt,
    string EmailLastError,
    DateTimeOffset? OpenedAt,
    DateTimeOffset? CompletedAt,
    Guid? FormSubmissionId,
    int? SubmissionNumber,
    bool CanResend);

public class ExternalFormInviteTrackingService(CMIForgeDbContext db)
{
    public async Task<List<ExternalFormInviteTrackingRow>> ListForFormAsync(Guid formDefinitionId, int take = 10)
    {
        var invites = await BaseQuery()
            .Where(x => x.FormDefinitionId == formDefinitionId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();

        return invites.Select(Map).ToList();
    }

    public async Task<List<ExternalFormInviteTrackingRow>> ListForContactAsync(Guid contactId, int take = 10)
    {
        var invites = await BaseQuery()
            .Where(x => x.RecipientContactId == contactId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();

        return invites.Select(Map).ToList();
    }

    public async Task<List<ExternalFormInviteTrackingRow>> ListForClientAsync(Guid clientId, int take = 10)
    {
        var contactIds = db.ClientContacts
            .Where(x => x.ClientId == clientId)
            .Select(x => x.ContactId);

        var invites = await BaseQuery()
            .Where(x => x.RecipientContactId.HasValue && contactIds.Contains(x.RecipientContactId.Value))
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();

        return invites.Select(Map).ToList();
    }

    public async Task<List<ExternalFormInviteTrackingRow>> ListForMatterAsync(Guid matterId, int take = 10)
    {
        var contactIds = db.MatterContacts
            .Where(x => x.MatterId == matterId)
            .Select(x => x.ContactId);

        var invites = await BaseQuery()
            .Where(x => x.RecipientContactId.HasValue && contactIds.Contains(x.RecipientContactId.Value))
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();

        return invites.Select(Map).ToList();
    }

    private IQueryable<ExternalFormInvite> BaseQuery()
    {
        return db.ExternalFormInvites
            .AsNoTracking()
            .Include(x => x.FormDefinition)
            .Include(x => x.FormSubmission)
            .Include(x => x.EmailOutboxMessages);
    }

    private static ExternalFormInviteTrackingRow Map(ExternalFormInvite invite)
    {
        var latestEmail = invite.EmailOutboxMessages
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        var status = invite.Status == ExternalFormInviteStatuses.Open && invite.ExpiresAt <= DateTimeOffset.UtcNow
            ? ExternalFormInviteStatuses.Expired
            : invite.Status;

        return new ExternalFormInviteTrackingRow(
            invite.Id,
            invite.FormDefinitionId,
            invite.FormDefinition?.Name ?? "External form",
            invite.RecipientName,
            invite.RecipientEmail,
            invite.CreatedAt,
            invite.ExpiresAt,
            status,
            invite.EmailQueuedAt,
            latestEmail?.Status ?? (invite.EmailQueuedAt.HasValue ? EmailOutboxStatuses.Pending : "Not queued"),
            latestEmail?.SentAt,
            latestEmail?.LastError ?? string.Empty,
            invite.OpenedAt,
            invite.CompletedAt,
            invite.FormSubmissionId,
            invite.FormSubmission?.SubmissionNumber,
            status != ExternalFormInviteStatuses.Completed);
    }
}
