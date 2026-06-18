using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Clients;

public class DetailsModel(MatterForgeDbContext db, PermissionService permissionService) : PageModel
{
    public Client? Client { get; private set; }

    public List<EntityChangeRequest> PendingChanges { get; private set; } = [];

    public bool CanEditEntities { get; private set; }

    public async Task OnGetAsync(Guid id)
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
    }
}
