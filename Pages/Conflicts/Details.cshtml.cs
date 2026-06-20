using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Conflicts;

public class DetailsModel(
    CMIForgeDbContext db,
    ConflictSearchService conflictSearchService,
    ConflictSearchArchiveService archiveService,
    CurrentUserService currentUserService,
    PermissionService permissionService) : PageModel
{
    public ConflictSearch? Search { get; private set; }

    public ConflictSearchArchive? Archive { get; private set; }

    public List<ConflictResultDisplayItem> Results { get; private set; } = [];

    public bool IsArchived => Archive is not null;

    public bool CanReview { get; private set; }

    public bool CanRun { get; private set; }

    public SelectList DecisionOptions { get; } = new(ConflictSearchDecisions.All);

    public IReadOnlyList<string> ResultStatusOptions { get; } = ConflictSearchDecisions.All;

    [BindProperty]
    public string Decision { get; set; } = ConflictSearchDecisions.Pending;

    [BindProperty]
    public string ReviewNotes { get; set; } = string.Empty;

    [BindProperty]
    public string AdditionalSearchTerms { get; set; } = string.Empty;

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

    public async Task<IActionResult> OnPostResultReviewAsync(Guid id, Guid resultId, string resultStatus, string resultNotes)
    {
        if (!await permissionService.HasAsync(PermissionKeys.ConflictsReview))
        {
            return Forbid();
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        await conflictSearchService.ApplyResultClearanceAsync(resultId, resultStatus, resultNotes, currentUser?.Id);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostBulkResultReviewAsync(Guid id, List<Guid> selectedResultIds, string bulkResultStatus, string bulkResultNotes)
    {
        if (!await permissionService.HasAsync(PermissionKeys.ConflictsReview))
        {
            return Forbid();
        }

        if (selectedResultIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Select at least one result to update.");
            await LoadSearchAsync(id);
            return Page();
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        await conflictSearchService.ApplyResultClearanceAsync(selectedResultIds, bulkResultStatus, bulkResultNotes, currentUser?.Id);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRerunAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.ConflictsRun))
        {
            return Forbid();
        }

        var search = await db.ConflictSearches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (search is null)
        {
            return NotFound();
        }

        await conflictSearchService.RerunSearchAsync(search, AdditionalSearchTerms);
        var results = search.Results.ToList();
        search.Results.Clear();

        db.ChangeTracker.Clear();
        db.ConflictSearches.Attach(search);
        var searchEntry = db.Entry(search);
        searchEntry.Property(x => x.SearchTerms).IsModified = true;
        searchEntry.Property(x => x.NormalizedTerms).IsModified = true;
        searchEntry.Property(x => x.Status).IsModified = true;
        searchEntry.Property(x => x.ReviewerDecision).IsModified = true;
        searchEntry.Property(x => x.ArchivedAt).IsModified = true;
        searchEntry.Property(x => x.UpdatedAt).IsModified = true;
        searchEntry.Property(x => x.AiSummary).IsModified = true;
        db.ConflictSearchResults.AddRange(results);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex) when (ex.Entries.All(entry => entry.Entity is ConflictSearchResult))
        {
            foreach (var entry in ex.Entries)
            {
                entry.State = EntityState.Detached;
            }

            await db.SaveChangesAsync();
        }

        return RedirectToPage(new { id });
    }

    private async Task LoadSearchAsync(Guid id)
    {
        CanReview = await permissionService.HasAsync(PermissionKeys.ConflictsReview);
        CanRun = await permissionService.HasAsync(PermissionKeys.ConflictsRun);
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
            .Include(x => x.Results)
                .ThenInclude(x => x.ClearedByUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Search is not null)
        {
            Archive = await archiveService.GetArchiveAsync(Search.Id);
            if (Archive is not null)
            {
                var payload = archiveService.DecompressPayload(Archive);
                Results = payload.Results
                    .Select(ConflictResultDisplayItem.FromArchive)
                    .ToList();
                CanReview = false;
                CanRun = false;
            }
            else
            {
                Results = Search.Results
                    .Select(ConflictResultDisplayItem.FromLive)
                    .ToList();
            }

            Decision = Search.ReviewerDecision;
            ReviewNotes = Search.ReviewNotes;
        }
    }
}

public sealed class ConflictResultDisplayItem
{
    public Guid Id { get; init; }

    public Guid? PartyId { get; init; }

    public string PartyName { get; init; } = string.Empty;

    public Guid? MatterId { get; init; }

    public int? MatterNumber { get; init; }

    public string MatterName { get; init; } = string.Empty;

    public Guid? ClientId { get; init; }

    public int? ClientNumber { get; init; }

    public string ClientName { get; init; } = string.Empty;

    public string SearchTerm { get; init; } = string.Empty;

    public string MatchedName { get; init; } = string.Empty;

    public string MatchedOn { get; init; } = string.Empty;

    public string MatchType { get; init; } = string.Empty;

    public string PartyRole { get; init; } = string.Empty;

    public int Score { get; init; }

    public string RiskLevel { get; init; } = ConflictRiskLevels.Low;

    public string Explanation { get; init; } = string.Empty;

    public string AiAssessment { get; init; } = string.Empty;

    public string ClearanceStatus { get; init; } = ConflictSearchDecisions.Pending;

    public string ClearanceNotes { get; init; } = string.Empty;

    public string ClearedByDisplayName { get; init; } = string.Empty;

    public DateTimeOffset? ClearedAt { get; init; }

    public static ConflictResultDisplayItem FromLive(ConflictSearchResult result)
    {
        return new ConflictResultDisplayItem
        {
            Id = result.Id,
            PartyId = result.PartyId,
            PartyName = result.Party?.Name ?? string.Empty,
            MatterId = result.MatterId,
            MatterNumber = result.Matter?.MatterNumber,
            MatterName = result.Matter?.Name ?? string.Empty,
            ClientId = result.ClientId,
            ClientNumber = result.Client?.ClientNumber,
            ClientName = result.Client?.Name ?? string.Empty,
            SearchTerm = result.SearchTerm,
            MatchedName = result.MatchedName,
            MatchedOn = result.MatchedOn,
            MatchType = result.MatchType,
            PartyRole = result.PartyRole,
            Score = result.Score,
            RiskLevel = result.RiskLevel,
            Explanation = result.Explanation,
            AiAssessment = result.AiAssessment,
            ClearanceStatus = result.ClearanceStatus,
            ClearanceNotes = result.ClearanceNotes,
            ClearedByDisplayName = result.ClearedByUser?.DisplayName ?? "System",
            ClearedAt = result.ClearedAt
        };
    }

    public static ConflictResultDisplayItem FromArchive(ConflictSearchArchiveResultPayload result)
    {
        return new ConflictResultDisplayItem
        {
            Id = result.Id,
            PartyId = result.PartyId,
            PartyName = result.PartyName,
            MatterId = result.MatterId,
            MatterNumber = result.MatterNumber,
            MatterName = result.MatterName,
            ClientId = result.ClientId,
            ClientNumber = result.ClientNumber,
            ClientName = result.ClientName,
            SearchTerm = result.SearchTerm,
            MatchedName = result.MatchedName,
            MatchedOn = result.MatchedOn,
            MatchType = result.MatchType,
            PartyRole = result.PartyRole,
            Score = result.Score,
            RiskLevel = result.RiskLevel,
            Explanation = result.Explanation,
            AiAssessment = result.AiAssessment,
            ClearanceStatus = result.ClearanceStatus,
            ClearanceNotes = result.ClearanceNotes,
            ClearedByDisplayName = result.ClearedByDisplayName,
            ClearedAt = result.ClearedAt
        };
    }
}
