using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Time;

public class DetailsModel(
    CMIForgeDbContext db,
    CurrentUserService currentUserService,
    PermissionService permissionService) : PageModel
{
    public TimeEntry? Entry { get; private set; }

    public bool CanCreate { get; private set; }

    public bool CanEdit { get; private set; }

    public bool CanApproveEntry { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var result = await LoadAsync(id);
        return result ?? Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        var result = await LoadAsync(id);
        if (result is not null)
        {
            return result;
        }

        if (Entry is null || !CanApproveEntry)
        {
            return Forbid();
        }

        var entry = await db.TimeEntries.FirstOrDefaultAsync(x => x.Id == id);
        if (entry is null)
        {
            return NotFound();
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        entry.Status = TimeEntryStatuses.Approved;
        entry.ApprovedAt = DateTimeOffset.UtcNow;
        entry.ApprovedByUserId = currentUser?.Id;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    private async Task<IActionResult?> LoadAsync(Guid id)
    {
        var canViewAll = await permissionService.HasAsync(PermissionKeys.TimeViewAll);
        var canViewOwn = await permissionService.HasAsync(PermissionKeys.TimeViewOwn);
        CanCreate = await permissionService.HasAsync(PermissionKeys.TimeCreate);
        var canEdit = await permissionService.HasAsync(PermissionKeys.TimeEdit);
        var canApprove = await permissionService.HasAsync(PermissionKeys.TimeApprove);

        Entry = await db.TimeEntries
            .Include(x => x.User)
            .Include(x => x.Client)
            .Include(x => x.Matter)
                .ThenInclude(x => x!.LeadPartner)
            .Include(x => x.TimePhase)
            .Include(x => x.TimeTask)
            .Include(x => x.ApprovedByUser)
            .Include(x => x.ExportedByUser)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (Entry is null)
        {
            return null;
        }

        var user = await currentUserService.GetCurrentUserAsync();
        var canApproveThisEntry = canApprove &&
            Entry.Status == TimeEntryStatuses.Submitted &&
            !Entry.ExportedAt.HasValue &&
            Entry.Matter?.RequiresTimeApproval == true &&
            Entry.Matter.LeadPartnerId.HasValue &&
            user?.Id == Entry.Matter.LeadPartnerId.Value;

        if (!canViewAll &&
            !(canViewOwn && user?.Id == Entry.UserId) &&
            !canApproveThisEntry)
        {
            return Forbid();
        }

        CanEdit = canEdit && !Entry.IsLocked;
        CanApproveEntry = canApproveThisEntry;

        return null;
    }
}
