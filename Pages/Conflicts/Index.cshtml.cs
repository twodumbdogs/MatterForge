using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Conflicts;

public class IndexModel(MatterForgeDbContext db, PermissionService permissionService) : PageModel
{
    public List<ConflictSearch> Searches { get; private set; } = [];

    public bool CanRunSearches { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.ConflictsView))
        {
            return Forbid();
        }

        CanRunSearches = await permissionService.HasAsync(PermissionKeys.ConflictsRun);
        Searches = await db.ConflictSearches
            .Include(x => x.FormSubmission)
                .ThenInclude(x => x!.FormDefinition)
            .Include(x => x.Matter)
                .ThenInclude(x => x!.Client)
            .Include(x => x.RequestedByUser)
            .Include(x => x.Results)
            .OrderByDescending(x => x.SearchNumber)
            .ToListAsync();

        return Page();
    }
}
