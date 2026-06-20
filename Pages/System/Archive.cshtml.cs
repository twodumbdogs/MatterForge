using System.ComponentModel.DataAnnotations;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.System;

public class ArchiveModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    AuditLogService auditLogService) : PageModel
{
    public List<Client> Clients { get; private set; } = [];

    public List<Matter> Matters { get; private set; } = [];

    public List<Party> Parties { get; private set; } = [];

    public List<Contact> Contacts { get; private set; } = [];

    public List<CMIForgeUser> Users { get; private set; } = [];

    public List<FormSubmission> Submissions { get; private set; } = [];

    [BindProperty]
    public RestoreArchiveInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRestoreAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var restored = await RestoreAsync();
        if (!restored)
        {
            return NotFound();
        }

        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Clients = await db.Clients
            .AsNoTracking()
            .Where(x => x.IsArchived)
            .OrderBy(x => x.ClientNumber)
            .Take(50)
            .ToListAsync();

        Matters = await db.Matters
            .AsNoTracking()
            .Include(x => x.Client)
            .Where(x => x.IsArchived)
            .OrderBy(x => x.MatterNumber)
            .Take(50)
            .ToListAsync();

        Parties = await db.Parties
            .AsNoTracking()
            .Where(x => x.IsArchived)
            .OrderBy(x => x.PartyNumber)
            .Take(50)
            .ToListAsync();

        Contacts = await db.Contacts
            .AsNoTracking()
            .Where(x => x.IsArchived)
            .OrderBy(x => x.ContactNumber)
            .Take(50)
            .ToListAsync();

        Users = await db.Users
            .AsNoTracking()
            .Where(x => x.IsArchived)
            .OrderBy(x => x.SystemId)
            .Take(50)
            .ToListAsync();

        Submissions = await db.FormSubmissions
            .AsNoTracking()
            .Include(x => x.FormDefinition)
            .Include(x => x.SubmitterUser)
            .Where(x => x.Status == SubmissionStatuses.Cancelled)
            .OrderByDescending(x => x.SubmissionNumber)
            .Take(50)
            .ToListAsync();
    }

    private async Task<bool> RestoreAsync()
    {
        switch (Input.EntityType)
        {
            case ArchiveEntityTypes.Client:
                var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == Input.EntityId && x.IsArchived);
                if (client is null)
                {
                    return false;
                }

                client.IsArchived = false;
                client.UpdatedAt = DateTimeOffset.UtcNow;
                await auditLogService.LogAsync("Client.Restored", "Client", client.Id, client.ClientNumber.ToString("D8"), $"Restored client {client.Name} from System Archive.");
                return true;

            case ArchiveEntityTypes.Matter:
                var matter = await db.Matters.FirstOrDefaultAsync(x => x.Id == Input.EntityId && x.IsArchived);
                if (matter is null)
                {
                    return false;
                }

                matter.IsArchived = false;
                matter.UpdatedAt = DateTimeOffset.UtcNow;
                await auditLogService.LogAsync("Matter.Restored", "Matter", matter.Id, matter.MatterNumber.ToString("D8"), $"Restored matter {matter.Name} from System Archive.");
                return true;

            case ArchiveEntityTypes.Party:
                var party = await db.Parties.FirstOrDefaultAsync(x => x.Id == Input.EntityId && x.IsArchived);
                if (party is null)
                {
                    return false;
                }

                party.IsArchived = false;
                party.UpdatedAt = DateTimeOffset.UtcNow;
                await auditLogService.LogAsync("Party.Restored", "Party", party.Id, party.PartyNumber.ToString("D8"), $"Restored party {party.Name} from System Archive.");
                return true;

            case ArchiveEntityTypes.Contact:
                var contact = await db.Contacts.FirstOrDefaultAsync(x => x.Id == Input.EntityId && x.IsArchived);
                if (contact is null)
                {
                    return false;
                }

                contact.IsArchived = false;
                contact.UpdatedAt = DateTimeOffset.UtcNow;
                await auditLogService.LogAsync("Contact.Restored", "Contact", contact.Id, contact.ContactNumber.ToString("D8"), $"Restored contact {contact.DisplayName} from System Archive.");
                return true;

            case ArchiveEntityTypes.User:
                var user = await db.Users.FirstOrDefaultAsync(x => x.Id == Input.EntityId && x.IsArchived);
                if (user is null)
                {
                    return false;
                }

                user.IsArchived = false;
                user.UpdatedAt = DateTimeOffset.UtcNow;
                await auditLogService.LogAsync("User.Restored", "User", user.Id, user.SystemId.ToString("D8"), $"Restored user {user.DisplayName} from System Archive.");
                return true;

            case ArchiveEntityTypes.Submission:
                var submission = await db.FormSubmissions.FirstOrDefaultAsync(x =>
                    x.Id == Input.EntityId &&
                    x.Status == SubmissionStatuses.Cancelled);
                if (submission is null)
                {
                    return false;
                }

                submission.Status = SubmissionStatuses.Submitted;
                await auditLogService.LogAsync(
                    "Submission.Restored",
                    "Submission",
                    submission.Id,
                    RecordNumbers.Submission(submission.SubmissionNumber),
                    $"Restored cancelled submission {RecordNumbers.Submission(submission.SubmissionNumber)} from System Archive.");
                return true;

            default:
                return false;
        }
    }
}

public class RestoreArchiveInput
{
    [Required]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    public Guid EntityId { get; set; }
}

public static class ArchiveEntityTypes
{
    public const string Client = "Client";
    public const string Matter = "Matter";
    public const string Party = "Party";
    public const string Contact = "Contact";
    public const string User = "User";
    public const string Submission = "Submission";
}
