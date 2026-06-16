using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Conflicts;

public class DetailsModel(
    MatterForgeDbContext db,
    ConflictSearchService conflictSearchService,
    CurrentUserService currentUserService,
    PermissionService permissionService) : PageModel
{
    public ConflictSearch? Search { get; private set; }

    public bool CanReview { get; private set; }

    public SelectList DecisionOptions { get; } = new(ConflictSearchDecisions.All);

    [BindProperty]
    public string Decision { get; set; } = ConflictSearchDecisions.Pending;

    [BindProperty]
    public string ReviewNotes { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.ConflictsView))
        {
            return Forbid();
        }

        await LoadSearchAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostReviewAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.ConflictsReview))
        {
            return Forbid();
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        await conflictSearchService.ApplyReviewDecisionAsync(id, Decision, ReviewNotes, currentUser?.Id);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRerunAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.ConflictsRun))
        {
            return Forbid();
        }

        var search = await db.ConflictSearches
            .Include(x => x.Results)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (search is null)
        {
            return NotFound();
        }

        await conflictSearchService.RunSearchAsync(search);
        await db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    private async Task LoadSearchAsync(Guid id)
    {
        CanReview = await permissionService.HasAsync(PermissionKeys.ConflictsReview);
        Search = await db.ConflictSearches
            .Include(x => x.FormSubmission)
                .ThenInclude(x => x!.FormDefinition)
            .Include(x => x.Matter)
                .ThenInclude(x => x!.Client)
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewedByUser)
            .Include(x => x.Results)
                .ThenInclude(x => x.Party)
            .Include(x => x.Results)
                .ThenInclude(x => x.Matter)
            .Include(x => x.Results)
                .ThenInclude(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Search is not null)
        {
            Decision = Search.ReviewerDecision;
            ReviewNotes = Search.ReviewNotes;
        }
    }
}
