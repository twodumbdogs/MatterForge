using System.ComponentModel.DataAnnotations;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Contacts;

public class DetailsModel(CMIForgeDbContext db, PermissionService permissionService, AuditLogService auditLogService) : PageModel
{
    public Contact? Contact { get; private set; }

    public List<AuditLog> AuditHistory { get; private set; } = [];

    public List<EntityChangeRequest> PendingChanges { get; private set; } = [];

    public List<SelectListItem> ClientOptions { get; private set; } = [];

    public List<SelectListItem> MatterOptions { get; private set; } = [];

    public SelectList ContactRoleOptions { get; } = new(ContactRoles.All);

    public bool CanEditEntities { get; private set; }

    [BindProperty]
    public AddClientContactInput ClientLinkInput { get; set; } = new();

    [BindProperty]
    public AddMatterContactInput MatterLinkInput { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        await LoadPageAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostClientLinkAsync(Guid id)
    {
        await LoadPageAsync(id);
        if (Contact is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var exists = await db.ClientContacts.AnyAsync(x =>
            x.ContactId == id &&
            x.ClientId == ClientLinkInput.ClientId &&
            x.Role == ClientLinkInput.Role);
        if (!exists)
        {
            db.ClientContacts.Add(new ClientContact
            {
                ContactId = id,
                ClientId = ClientLinkInput.ClientId,
                Role = ClientLinkInput.Role,
                IsPrimary = ClientLinkInput.IsPrimary,
                Notes = ClientLinkInput.Notes?.Trim() ?? string.Empty
            });
            await db.SaveChangesAsync();
            await auditLogService.LogAsync(
                "Contact.ClientLinkAdded",
                "Contact",
                id,
                Contact.ContactNumber.ToString("D8"),
                $"Linked contact {Contact.DisplayName} to client as {ClientLinkInput.Role}.",
                new { ClientLinkInput.ClientId, ClientLinkInput.IsPrimary });
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostMatterLinkAsync(Guid id)
    {
        await LoadPageAsync(id);
        if (Contact is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var exists = await db.MatterContacts.AnyAsync(x =>
            x.ContactId == id &&
            x.MatterId == MatterLinkInput.MatterId &&
            x.Role == MatterLinkInput.Role);
        if (!exists)
        {
            db.MatterContacts.Add(new MatterContact
            {
                ContactId = id,
                MatterId = MatterLinkInput.MatterId,
                Role = MatterLinkInput.Role,
                IsPrimary = MatterLinkInput.IsPrimary,
                Notes = MatterLinkInput.Notes?.Trim() ?? string.Empty
            });
            await db.SaveChangesAsync();
            await auditLogService.LogAsync(
                "Contact.MatterLinkAdded",
                "Contact",
                id,
                Contact.ContactNumber.ToString("D8"),
                $"Linked contact {Contact.DisplayName} to matter as {MatterLinkInput.Role}.",
                new { MatterLinkInput.MatterId, MatterLinkInput.IsPrimary });
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostArchiveAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        var contact = await db.Contacts.FirstOrDefaultAsync(x => x.Id == id);
        if (contact is null)
        {
            return NotFound();
        }

        contact.IsArchived = !contact.IsArchived;
        contact.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            contact.IsArchived ? "Contact.Archived" : "Contact.Restored",
            "Contact",
            contact.Id,
            contact.ContactNumber.ToString("D8"),
            $"{(contact.IsArchived ? "Archived" : "Restored")} contact {contact.DisplayName}.");

        return RedirectToPage(new { id });
    }

    private async Task LoadPageAsync(Guid id)
    {
        CanEditEntities = await permissionService.HasAsync(PermissionKeys.EntitiesEdit);
        Contact = await db.Contacts
            .Include(x => x.ClientLinks)
                .ThenInclude(x => x.Client)
            .Include(x => x.MatterLinks)
                .ThenInclude(x => x.Matter)
                    .ThenInclude(x => x!.Client)
            .FirstOrDefaultAsync(x => x.Id == id);

        ClientOptions = await db.Clients
            .Where(x => !x.IsArchived)
            .OrderBy(x => x.ClientNumber)
            .Select(x => new SelectListItem($"{x.ClientNumber:D8} - {x.Name}", x.Id.ToString()))
            .ToListAsync();

        MatterOptions = await db.Matters
            .Where(x => !x.IsArchived)
            .Include(x => x.Client)
            .OrderBy(x => x.MatterNumber)
            .Select(x => new SelectListItem($"{x.MatterNumber:D8} - {x.Name} / {x.Client!.Name}", x.Id.ToString()))
            .ToListAsync();

        AuditHistory = Contact is null
            ? []
            : await auditLogService.ListForEntityAsync("Contact", id);

        PendingChanges = await db.EntityChangeRequests
            .Include(x => x.RequestedByUser)
            .Where(x => x.EntityType == EntityChangeService.ContactEntityType &&
                x.EntityId == id &&
                x.Status == EntityChangeRequestStatuses.Pending)
            .OrderBy(x => x.RequestedAt)
            .ToListAsync();
    }
}

public class AddClientContactInput
{
    [Required]
    [Display(Name = "Client")]
    public Guid ClientId { get; set; }

    public string Role { get; set; } = ContactRoles.Primary;

    [Display(Name = "Primary")]
    public bool IsPrimary { get; set; }

    public string? Notes { get; set; }
}

public class AddMatterContactInput
{
    [Required]
    [Display(Name = "Matter")]
    public Guid MatterId { get; set; }

    public string Role { get; set; } = ContactRoles.MatterContact;

    [Display(Name = "Primary")]
    public bool IsPrimary { get; set; }

    public string? Notes { get; set; }
}
