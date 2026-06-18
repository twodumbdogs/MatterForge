using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Submissions;

public class IndexModel(
    MatterForgeDbContext db,
    CurrentUserService currentUserService,
    PermissionService permissionService) : PageModel
{
    public List<FormSubmission> Submissions { get; private set; } = [];

    public MatterForgeUser? CurrentUser { get; private set; }

    public bool CanViewAll { get; private set; }

    public int MySubmissionCount { get; private set; }

    public int AllSubmissionCount { get; private set; }

    public bool CanSubmitForms { get; private set; }

    public List<SubmissionFormOption> PublishedForms { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string View { get; set; } = "mine";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SubmissionsViewOwn) &&
            !await permissionService.HasAsync(PermissionKeys.SubmissionsViewAll))
        {
            return Forbid();
        }

        CurrentUser = await currentUserService.GetCurrentUserAsync();
        CanViewAll = await permissionService.HasAsync(PermissionKeys.SubmissionsViewAll);
        CanSubmitForms = await permissionService.HasAsync(PermissionKeys.FormsSubmit);
        var currentUserId = CurrentUser?.Id;

        if (View is not ("mine" or "all"))
        {
            View = "mine";
        }

        if (View == "all" && !CanViewAll)
        {
            View = "mine";
        }

        MySubmissionCount = currentUserId.HasValue
            ? await db.FormSubmissions.CountAsync(x => x.SubmitterUserId == currentUserId.Value)
            : 0;
        AllSubmissionCount = await db.FormSubmissions.CountAsync();

        if (CanSubmitForms)
        {
            PublishedForms = await db.FormDefinitions
                .AsNoTracking()
                .Where(x => x.IsActive && x.Versions.Any(version => version.IsPublished))
                .OrderBy(x => x.Name)
                .Select(x => new SubmissionFormOption(
                    x.Id,
                    x.Name,
                    x.Versions
                        .Where(version => version.IsPublished)
                        .Max(version => version.VersionNumber)))
                .ToListAsync();
        }

        var submissionsQuery = db.FormSubmissions.AsQueryable();
        if (View == "mine")
        {
            submissionsQuery = currentUserId.HasValue
                ? submissionsQuery.Where(x => x.SubmitterUserId == currentUserId.Value)
                : submissionsQuery.Where(x => false);
        }

        Submissions = await submissionsQuery
            .Include(x => x.Client)
            .Include(x => x.Matter)
            .Include(x => x.SubmitterUser)
            .OrderByDescending(x => x.SubmissionNumber)
            .ToListAsync();

        return Page();
    }
}

public sealed record SubmissionFormOption(Guid Id, string Name, int VersionNumber);
