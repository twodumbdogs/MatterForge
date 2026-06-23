using System.ComponentModel.DataAnnotations;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Conflicts;

public class CreateModel(
    CMIForgeDbContext db,
    ConflictSearchService conflictSearchService,
    CurrentUserService currentUserService,
    PermissionService permissionService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid? SubmissionId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? MatterId { get; set; }

    [BindProperty]
    public ConflictSearchInput Input { get; set; } = new();

    public List<MatterContextOption> MatterOptions { get; private set; } = [];

    public FormSubmission? SourceSubmission { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.ConflictsRun))
        {
            return Forbid();
        }

        await LoadOptionsAsync();
        await PrefillAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.ConflictsRun))
        {
            return Forbid();
        }

        await LoadOptionsAsync();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        var search = await conflictSearchService.CreateAndRunSearchAsync(
            Input.SearchName,
            Input.SearchTerms,
            SubmissionId,
            Input.MatterId,
            currentUser?.Id);

        return RedirectToPage("./Details", new { id = search.Id });
    }

    private async Task LoadOptionsAsync()
    {
        MatterOptions = await db.Matters
            .Include(x => x.Client)
            .OrderByDescending(x => x.MatterNumber)
            .Select(x => new MatterContextOption(
                x.Id,
                $"{x.MatterNumber:D8} - {x.Name} / {x.Client!.Name}",
                x.Name))
            .ToListAsync();
    }

    private async Task PrefillAsync()
    {
        Input.MatterId = MatterId;

        if (SubmissionId.HasValue)
        {
            SourceSubmission = await db.FormSubmissions
                .Include(x => x.FormDefinition)
                .Include(x => x.FormVersion)
                .FirstOrDefaultAsync(x => x.Id == SubmissionId.Value);

            if (SourceSubmission?.FormVersion is not null)
            {
                var answers = SubmissionAnswerReader.Read(SourceSubmission.DataJson);
                var terms = new[]
                    {
                        SubmissionAnswerReader.FirstValue(answers, "clientName", "client", "companyName"),
                        SubmissionAnswerReader.FirstValue(answers, "matterName", "matter"),
                        SubmissionAnswerReader.FirstValue(answers, "adverseParty", "adverseParties", "opposingParty", "opposingParties"),
                        SubmissionAnswerReader.FirstValue(answers, "opposingCounsel", "counsel"),
                        SubmissionAnswerReader.FirstValue(answers, "relatedParty", "relatedParties", "affiliate", "parentCompany", "subsidiary")
                    }
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Input.SearchName = $"{RecordNumbers.Submission(SourceSubmission.SubmissionNumber)} Search";
                Input.SearchTerms = string.Join(Environment.NewLine, terms);
            }
        }
        else if (MatterId.HasValue)
        {
            var matter = await db.Matters
                .Include(x => x.Client)
                .FirstOrDefaultAsync(x => x.Id == MatterId.Value);
            if (matter is not null)
            {
                Input.SearchName = $"Conflicts - {matter.Name}";
                Input.SearchTerms = string.Join(Environment.NewLine, new[] { matter.Client?.Name, matter.Name }.Where(x => !string.IsNullOrWhiteSpace(x)));
            }
        }

        if (string.IsNullOrWhiteSpace(Input.SearchName))
        {
            Input.SearchName = "Conflict search";
        }
    }
}

public class ConflictSearchInput
{
    [Required]
    [Display(Name = "Search name")]
    public string SearchName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Search terms")]
    public string SearchTerms { get; set; } = string.Empty;

    [Display(Name = "Matter context")]
    public Guid? MatterId { get; set; }
}

public sealed record MatterContextOption(Guid Id, string Label, string MatterName);
