using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Clients;

public class DetailsModel(
    MatterForgeDbContext db,
    PermissionService permissionService,
    EntityNoteService entityNoteService,
    CurrentUserService currentUserService) : PageModel
{
    public Client? Client { get; private set; }

    public List<EntityNote> Notes { get; private set; } = [];

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
    }
}
