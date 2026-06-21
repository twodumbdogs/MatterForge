using System.ComponentModel.DataAnnotations;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Forms;

public class SendModel(
    CMIForgeDbContext db,
    CurrentUserService currentUserService,
    PermissionService permissionService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public SendFormInput Input { get; set; } = new();

    public FormDefinition? Form { get; private set; }

    public int VersionNumber { get; private set; }

    public List<SelectListItem> ContactOptions { get; private set; } = [];

    public List<SelectListItem> PartnerOptions { get; private set; } = [];

    public List<ExternalFormInvite> RecentInvites { get; private set; } = [];

    public string? GeneratedLink { get; private set; }

    public string? ResultMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.FormsSubmit))
        {
            return Forbid();
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.FormsSubmit))
        {
            return Forbid();
        }

        await LoadAsync();
        if (Form is null)
        {
            return Page();
        }

        var latestVersion = Form.Versions
            .Where(x => x.IsPublished)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefault();
        if (latestVersion is null)
        {
            ModelState.AddModelError(string.Empty, "This form does not have a published version to send.");
            return Page();
        }

        var contact = await db.Contacts
            .FirstOrDefaultAsync(x => x.Id == Input.ContactId && !x.IsArchived);
        if (contact is null || string.IsNullOrWhiteSpace(contact.Email))
        {
            ModelState.AddModelError(nameof(Input.ContactId), "Choose an active contact with an email address.");
        }

        if (Input.LeadPartnerId.HasValue && !await IsPartnerAsync(Input.LeadPartnerId.Value))
        {
            ModelState.AddModelError(nameof(Input.LeadPartnerId), "Choose a user with the Partner role.");
        }

        Input.ExpiresInDays = Math.Clamp(Input.ExpiresInDays, 1, 30);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var token = ExternalFormInviteTokenService.GenerateToken();
        var invite = new ExternalFormInvite
        {
            FormDefinitionId = Form.Id,
            FormVersionId = latestVersion.Id,
            RecipientContactId = contact!.Id,
            RecipientName = contact.DisplayName,
            RecipientEmail = contact.Email.Trim(),
            SenderUserId = (await currentUserService.GetCurrentUserAsync())?.Id,
            LeadPartnerId = Input.LeadPartnerId,
            TokenHash = ExternalFormInviteTokenService.HashToken(token),
            Status = ExternalFormInviteStatuses.Open,
            Message = Input.Message?.Trim() ?? string.Empty,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(Input.ExpiresInDays)
        };

        db.ExternalFormInvites.Add(invite);
        await db.SaveChangesAsync();

        GeneratedLink = Url.Page(
            "/External/Forms/Submit",
            null,
            new { token },
            Request.Scheme);
        if (string.IsNullOrWhiteSpace(GeneratedLink))
        {
            ModelState.AddModelError(string.Empty, "The secure link could not be generated.");
            return Page();
        }

        var emailQueued = await TryQueueEmailAsync(invite, GeneratedLink);
        await auditLogService.LogAsync(
            "ExternalFormInvite.Created",
            "ExternalFormInvite",
            invite.Id,
            null,
            $"Created external form invite for {invite.RecipientName}.",
            new
            {
                invite.FormDefinitionId,
                invite.FormVersionId,
                invite.RecipientContactId,
                invite.RecipientEmail,
                invite.ExpiresAt,
                EmailQueued = emailQueued
            });

        ResultMessage = emailQueued
            ? "Invite created and queued for email delivery."
            : "Invite created. Email is not enabled/configured, so copy the secure link below.";

        Input = new SendFormInput
        {
            ExpiresInDays = 7,
            Message = "Please complete this secure intake form so we can gather the information needed for review."
        };
        await LoadRecentInvitesAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        Form = await db.FormDefinitions
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == Id && x.IsActive);

        VersionNumber = Form?.Versions
            .Where(x => x.IsPublished)
            .Select(x => x.VersionNumber)
            .DefaultIfEmpty()
            .Max() ?? 0;

        ContactOptions = await db.Contacts
            .Where(x => !x.IsArchived && x.Email != string.Empty)
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem($"{x.DisplayName} <{x.Email}>", x.Id.ToString()))
            .ToListAsync();

        PartnerOptions = await db.Users
            .Where(x => x.IsActive && !x.IsArchived)
            .Where(x => x.Roles.Any(role => role.SecurityRole != null && role.SecurityRole.Key == SecurityRoleKeys.Partner && role.SecurityRole.IsActive))
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem($"{x.DisplayName} ({x.SystemId:D8})", x.Id.ToString()))
            .ToListAsync();

        if (string.IsNullOrWhiteSpace(Input.Message))
        {
            Input.Message = "Please complete this secure intake form so we can gather the information needed for review.";
        }

        if (Input.ExpiresInDays <= 0)
        {
            Input.ExpiresInDays = 7;
        }

        await LoadRecentInvitesAsync();
    }

    private async Task LoadRecentInvitesAsync()
    {
        RecentInvites = await db.ExternalFormInvites
            .AsNoTracking()
            .Include(x => x.RecipientContact)
            .Include(x => x.FormSubmission)
            .Where(x => x.FormDefinitionId == Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync();
    }

    private Task<bool> IsPartnerAsync(Guid userId)
    {
        return db.Users.AnyAsync(x =>
            x.Id == userId &&
            x.IsActive &&
            !x.IsArchived &&
            x.Roles.Any(role => role.SecurityRole != null && role.SecurityRole.Key == SecurityRoleKeys.Partner && role.SecurityRole.IsActive));
    }

    private async Task<bool> TryQueueEmailAsync(ExternalFormInvite invite, string link)
    {
        var settings = await db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Category == "Email" || x.Key.StartsWith("Email."))
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

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
            Subject = $"Please complete {Form?.Name ?? "your CMIForge form"}",
            Body = BuildEmailBody(invite, link),
            IsBodyHtml = false
        });

        invite.EmailQueuedAt = DateTimeOffset.UtcNow;
        invite.UpdatedAt = invite.EmailQueuedAt.Value;
        await db.SaveChangesAsync();
        return true;
    }

    private string BuildEmailBody(ExternalFormInvite invite, string link)
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

public class SendFormInput
{
    [Required]
    [Display(Name = "Client contact")]
    public Guid? ContactId { get; set; }

    [Display(Name = "Lead partner")]
    public Guid? LeadPartnerId { get; set; }

    [Range(1, 30)]
    [Display(Name = "Expires in days")]
    public int ExpiresInDays { get; set; } = 7;

    [MaxLength(2000)]
    public string? Message { get; set; }
}
