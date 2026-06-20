using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Conflicts;

public class IndexModel(CMIForgeDbContext db, PermissionService permissionService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    public List<ConflictSearchListItem> Searches { get; private set; } = [];

    public bool CanRunSearches { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.ConflictsView))
        {
            return Forbid();
        }

        CanRunSearches = await permissionService.HasAsync(PermissionKeys.ConflictsRun);
        var trimmedQuery = Query?.Trim();
        var normalizedQuery = string.IsNullOrWhiteSpace(trimmedQuery)
            ? string.Empty
            : ConflictSearchService.NormalizeName(trimmedQuery);

        var liveQuery = db.ConflictSearches
            .AsNoTracking()
            .Include(x => x.FormSubmission)
                .ThenInclude(x => x!.FormDefinition)
            .Include(x => x.Matter)
                .ThenInclude(x => x!.Client)
            .Include(x => x.RequestedByUser)
            .Include(x => x.Results)
            .Where(x => x.ArchivedAt == null);

        var archiveQuery = db.ConflictSearchArchives
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(trimmedQuery))
        {
            liveQuery = liveQuery.Where(x =>
                x.SearchName.Contains(trimmedQuery) ||
                x.SearchTerms.Contains(trimmedQuery) ||
                x.NormalizedTerms.Contains(normalizedQuery) ||
                x.ReviewNotes.Contains(trimmedQuery) ||
                x.AiSummary.Contains(trimmedQuery) ||
                (x.Matter != null && x.Matter.Name.Contains(trimmedQuery)) ||
                (x.Matter != null && x.Matter.Client != null && x.Matter.Client.Name.Contains(trimmedQuery)) ||
                (x.FormSubmission != null && x.FormSubmission.FormDefinition != null && x.FormSubmission.FormDefinition.Name.Contains(trimmedQuery)));

            archiveQuery = archiveQuery.Where(x =>
                x.SearchableText.Contains(trimmedQuery) ||
                x.NormalizedSearchableText.Contains(normalizedQuery));
        }

        var liveSearches = await liveQuery.ToListAsync();
        var archivedSearches = await archiveQuery.ToListAsync();

        Searches = liveSearches
            .Select(ConflictSearchListItem.FromLive)
            .Concat(archivedSearches.Select(ConflictSearchListItem.FromArchive))
            .OrderByDescending(x => x.SearchNumber)
            .ToList();

        return Page();
    }
}

public sealed class ConflictSearchListItem
{
    public Guid Id { get; init; }

    public int SearchNumber { get; init; }

    public string SearchName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string ReviewerDecision { get; init; } = string.Empty;

    public int ResultCount { get; init; }

    public string? HighestRiskLevel { get; init; }

    public string RequestedBy { get; init; } = "System";

    public DateTimeOffset CreatedAt { get; init; }

    public bool IsArchived { get; init; }

    public int? SubmissionNumber { get; init; }

    public string FormName { get; init; } = string.Empty;

    public int? MatterNumber { get; init; }

    public string MatterName { get; init; } = string.Empty;

    public static ConflictSearchListItem FromLive(ConflictSearch search)
    {
        var topRisk = search.Results
            .OrderByDescending(x => Array.IndexOf(ConflictRiskLevels.All, x.RiskLevel))
            .ThenByDescending(x => x.Score)
            .FirstOrDefault();

        return new ConflictSearchListItem
        {
            Id = search.Id,
            SearchNumber = search.SearchNumber,
            SearchName = search.SearchName,
            Status = search.Status,
            ReviewerDecision = search.ReviewerDecision,
            ResultCount = search.Results.Count,
            HighestRiskLevel = topRisk?.RiskLevel,
            RequestedBy = search.RequestedByUser?.DisplayName ?? "System",
            CreatedAt = search.CreatedAt,
            SubmissionNumber = search.FormSubmission?.SubmissionNumber,
            FormName = search.FormSubmission?.FormDefinition?.Name ?? string.Empty,
            MatterNumber = search.Matter?.MatterNumber,
            MatterName = search.Matter?.Name ?? string.Empty
        };
    }

    public static ConflictSearchListItem FromArchive(ConflictSearchArchive archive)
    {
        return new ConflictSearchListItem
        {
            Id = archive.ConflictSearchId,
            SearchNumber = archive.SearchNumber,
            SearchName = archive.SearchName,
            Status = archive.Status,
            ReviewerDecision = archive.ReviewerDecision,
            ResultCount = archive.ResultCount,
            HighestRiskLevel = archive.ResultCount == 0 ? null : archive.HighestRiskLevel,
            RequestedBy = string.IsNullOrWhiteSpace(archive.RequestedByDisplayName) ? "System" : archive.RequestedByDisplayName,
            CreatedAt = archive.CreatedAt,
            IsArchived = true,
            SubmissionNumber = archive.SubmissionNumber,
            FormName = archive.FormName,
            MatterNumber = archive.MatterNumber,
            MatterName = archive.MatterName
        };
    }
}
