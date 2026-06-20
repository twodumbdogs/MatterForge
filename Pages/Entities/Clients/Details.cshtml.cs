using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Clients;

public class DetailsModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    EntityNoteService entityNoteService,
    CurrentUserService currentUserService,
    AuditLogService auditLogService) : PageModel
{
    public Client? Client { get; private set; }

    public List<EntityNote> Notes { get; private set; } = [];

    public List<AuditLog> AuditHistory { get; private set; } = [];

    public List<EntityChangeRequest> PendingChanges { get; private set; } = [];

    public bool CanEditEntities { get; private set; }

    [BindProperty]
    public string NewNote { get; set; } = string.Empty;

    public async Task OnGetAsync(Guid id)
    {
        await LoadPageAsync(id);
    }

    public async Task<IActionResult> OnPostNoteAsync(Guid id)
    {
        var clientExists = await db.Clients.AnyAsync(x => x.Id == id);
        if (!clientExists)
        {
            return NotFound();
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        await entityNoteService.AddAsync(EntityNoteService.ClientEntityType, id, NewNote, currentUser?.Id);
        await auditLogService.LogAsync("Client.NoteAdded", "Client", id, null, "Added client discussion note.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostArchiveAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == id);
        if (client is null)
        {
            return NotFound();
        }

        client.IsArchived = !client.IsArchived;
        client.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            client.IsArchived ? "Client.Archived" : "Client.Restored",
            "Client",
            client.Id,
            client.ClientNumber.ToString("D8"),
            $"{(client.IsArchived ? "Archived" : "Restored")} client {client.Name}.");

        return RedirectToPage(new { id });
    }

    private async Task LoadPageAsync(Guid id)
    {
        CanEditEntities = await permissionService.HasAsync(PermissionKeys.EntitiesEdit);
        Client = await db.Clients
            .Include(x => x.Matters)
                .ThenInclude(x => x.ResponsibleUser)
            .Include(x => x.Contacts)
                .ThenInclude(x => x.Contact)
            .FirstOrDefaultAsync(x => x.Id == id);

        PendingChanges = await db.EntityChangeRequests
            .Include(x => x.RequestedByUser)
            .Where(x => x.EntityType == EntityChangeService.ClientEntityType &&
                x.EntityId == id &&
                x.Status == EntityChangeRequestStatuses.Pending)
            .OrderBy(x => x.RequestedAt)
            .ToListAsync();

        Notes = Client is null
            ? []
            : await entityNoteService.ListAsync(EntityNoteService.ClientEntityType, id);

        AuditHistory = Client is null
            ? []
            : await auditLogService.ListForEntityAsync("Client", id);
    }
}