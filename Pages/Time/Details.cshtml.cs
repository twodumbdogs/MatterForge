using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Time;

public class DetailsModel(
    MatterForgeDbContext db,
    CurrentUserService currentUserService,
    PermissionService permissionService) : PageModel
{
    public TimeEntry? Entry { get; private set; }

    public bool CanCreate { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var canViewAll = await permissionService.HasAsync(PermissionKeys.TimeViewAll);
        var canViewOwn = await permissionService.HasAsync(PermissionKeys.TimeViewOwn);
        CanCreate = await permissionService.HasAsync(PermissionKeys.TimeCreate);
        if (!canViewAll && !canViewOwn)
        {
            return Forbid();
        }

        Entry = await db.TimeEntries
            .Include(x => x.User)
            .Include(x => x.Client)
            .Include(x => x.Matter)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (Entry is null)
        {
            return Page();
        }

        if (!canViewAll)
        {
            var currentUser = await currentUserService.GetCurrentUserAsync();
            if (currentUser?.Id != Entry.UserId)
            {
                return Forbid();
            }
        }

        return Page();
    }
}
