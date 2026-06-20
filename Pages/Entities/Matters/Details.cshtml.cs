using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Matters;

public class DetailsModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    CurrentUserService currentUserService,
    EntityNoteService entityNoteService,
    AuditLogService auditLogService) : PageModel
{
    public Matter? Matter { get; private set; }

    public List<EntityNote> Notes { get; private set; } = [];

    public List<AuditLog> AuditHistory { get; private set; } = [];

    public List<EntityChangeRequest> PendingChanges { get; private set; } = [];

    public bool CanEditEntities { get; private set; }

    public bool CanRunConflicts { get; private set; }

    public bool CanRecordTime { get; private set; }

    public bool CanViewTime { get; private set; }

    public bool CanViewAllTime { get; private set; }

    public decimal TotalTimeHours { get; private set; }

    [BindProperty]
    public string NewNote { get; set; } = string.Empty;

    public async Task OnGetAsync(Guid id)
    {
        await LoadPageAsync(id);
    }

    public async Task<IActionResult> OnPostNoteAsync(Guid id)
    {
        var matterExists = await db.Matters.AnyAsync(x => x.Id == id);
        if (!matterExists)
        {
            return NotFound();
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        await entityNoteService.AddAsync(EntityNoteService.MatterEntityType, id, NewNote, currentUser?.Id);
        await auditLogService.LogAsync("Matter.NoteAdded", "Matter", id, null, "Added matter discussion note.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostArchiveAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        var matter = await db.Matters.FirstOrDefaultAsync(x => x.Id == id);
        if (matter is null)
        {
            return NotFound();
        }

        matter.IsArchived = !matter.IsArchived;
        matter.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            matter.IsArchived ? "Matter.Archived" : "Matter.Restored",
            "Matter",
            matter.Id,
            matter.MatterNumber.ToString("D8"),
            $"{(matter.IsArchived ? "Archived" : "Restored")} matter {matter.Name}.");

        return RedirectToPage(new { id });
    }

    private async Task LoadPageAsync(Guid id)
    {
        CanRunConflicts = await permissionService.HasAsync(PermissionKeys.ConflictsRun);
        CanEditEntities = await permissionService.HasAsync(PermissionKeys.EntitiesEdit);
        CanRecordTime = await permissionService.HasAsync(PermissionKeys.TimeCreate);
        CanViewAllTime = await permissionService.HasAsync(PermissionKeys.TimeViewAll);
        CanViewTime = CanViewAllTime || await permissionService.HasAsync(PermissionKeys.TimeViewOwn);
        Matter = await db.Matters
            .Include(x => x.Client)
            .Include(x => x.ResponsibleUser)
            .Include(x => x.LeadPartner)
            .Include(x => x.TimeCodeSet)
            .Include(x => x.Parties)
                .ThenInclude(x => x.Party)
            .Include(x => x.Contacts)
                .ThenInclude(x => x.Contact)
            .Include(x => x.TimeEntries)
                .ThenInclude(x => x.User)
            .Include(x => x.TimeEntries)
                .ThenInclude(x => x.TimePhase)
            .Include(x => x.TimeEntries)
                .ThenInclude(x => x.TimeTask)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Matter is not null)
        {
            if (CanViewTime && !CanViewAllTime)
            {
                var currentUser = await currentUserService.GetCurrentUserAsync();
                Matter.TimeEntries = currentUser is null
                    ? []
                    : Matter.TimeEntries.Where(x => x.UserId == currentUser.Id).ToList();
            }

            TotalTimeHours = Matter.TimeEntries.Sum(x => x.Minutes) / 60m;
        }

        PendingChanges = await db.EntityChangeRequests
            .Include(x => x.RequestedByUser)
            .Where(x => x.EntityType == EntityChangeService.MatterEntityType &&
                x.EntityId == id &&
                x.Status == EntityChangeRequestStatuses.Pending)
            .OrderBy(x => x.RequestedAt)
            .ToListAsync();

        Notes = Matter is null
            ? []
            : await entityNoteService.ListAsync(EntityNoteService.MatterEntityType, id);

        AuditHistory = Matter is null
            ? []
            : await auditLogService.ListForEntityAsync("Matter", id);
    }
}
