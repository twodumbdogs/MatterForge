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

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public List<ConflictSearchListItem> Searches { get; private set; } = [];

    public RecordPage SearchPagination { get; private set; } = RecordPage.Empty;

    public Dictionary<string, string> SearchRouteValues { get; private set; } = [];

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

        SearchRouteValues = string.IsNullOrWhiteSpace(trimmedQuery)
            ? []
            : new Dictionary<string, string> { [nameof(Query)] = trimmedQuery };

        var liveCount = await liveQuery.CountAsync();
        var archiveCount = await archiveQuery.CountAsync();
        SearchPagination = RecordPage.Create(PageNumber, liveCount + archiveCount);
        var candidateLimit = SearchPagination.Skip + SearchPagination.PageSize;

        var liveRows = await liveQuery
            .OrderByDescending(x => x.SearchNumber)
            .Take(candidateLimit)
            .Select(x => new ConflictSearchListRow(
                x.Id,
                x.SearchNumber,
                x.SearchName,
                x.Status,
                x.ReviewerDecision,
                x.RequestedByUser == null ? "System" : x.RequestedByUser.DisplayName,
                x.CreatedAt,
                false,
                x.FormSubmission == null ? null : (int?)x.FormSubmission.SubmissionNumber,
                x.FormSubmission == null || x.FormSubmission.FormDefinition == null ? string.Empty : x.FormSubmission.FormDefinition.Name,
                x.Matter == null ? null : (int?)x.Matter.MatterNumber,
                x.Matter == null ? string.Empty : x.Matter.Name,
                0,
                null))
            .ToListAsync();

        var archiveRows = await archiveQuery
            .OrderByDescending(x => x.SearchNumber)
            .Take(candidateLimit)
            .Select(x => new ConflictSearchListRow(
                x.ConflictSearchId,
                x.SearchNumber,
                x.SearchName,
                x.Status,
                x.ReviewerDecision,
                string.IsNullOrWhiteSpace(x.RequestedByDisplayName) ? "System" : x.RequestedByDisplayName,
                x.CreatedAt,
                true,
                x.SubmissionNumber,
                x.FormName,
                x.MatterNumber,
                x.MatterName,
                x.ResultCount,
                x.ResultCount == 0 ? null : x.HighestRiskLevel))
            .ToListAsync();

        var livePageIds = liveRows
            .Concat(archiveRows)
            .OrderByDescending(x => x.SearchNumber)
            .Skip(SearchPagination.Skip)
            .Take(SearchPagination.PageSize)
            .Where(x => !x.IsArchived)
            .Select(x => x.Id)
            .ToList();

        var liveResultSummaries = livePageIds.Count == 0
            ? []
            : await db.ConflictSearchResults
                .AsNoTracking()
                .Where(x => livePageIds.Contains(x.ConflictSearchId))
                .GroupBy(x => x.ConflictSearchId)
                .Select(x => new ConflictSearchResultSummary(
                    x.Key,
                    x.Count(),
                    x.OrderByDescending(y => y.RiskLevel == ConflictRiskLevels.Critical ? 4 :
                        y.RiskLevel == ConflictRiskLevels.High ? 3 :
                        y.RiskLevel == ConflictRiskLevels.Medium ? 2 :
                        y.RiskLevel == ConflictRiskLevels.Low ? 1 : 0)
                        .ThenByDescending(y => y.Score)
                        .Select(y => y.RiskLevel)
                        .FirstOrDefault()))
                .ToListAsync();

        var summariesBySearchId = liveResultSummaries.ToDictionary(x => x.SearchId);
        Searches = liveRows
            .Concat(archiveRows)
            .OrderByDescending(x => x.SearchNumber)
            .Skip(SearchPagination.Skip)
            .Take(SearchPagination.PageSize)
            .Select(x => ConflictSearchListItem.FromRow(x, summariesBySearchId.GetValueOrDefault(x.Id)))
            .ToList();

        return Page();
    }
}

public sealed record ConflictSearchListRow(
    Guid Id,
    int SearchNumber,
    string SearchName,
    string Status,
    string ReviewerDecision,
    string RequestedBy,
    DateTimeOffset CreatedAt,
    bool IsArchived,
    int? SubmissionNumber,
    string FormName,
    int? MatterNumber,
    string MatterName,
    int ResultCount,
    string? HighestRiskLevel);

public sealed record ConflictSearchResultSummary(Guid SearchId, int ResultCount, string? HighestRiskLevel);

public sealed class ConflictSearchListItem
{
    public Guid Id { get; init; }

    public int SearchNumber { get; init; }

    public string SearchName { get; init; } = string.Empty;

    public string DisplaySearchName => RecordNumbers.ConflictSearchDisplayName(SearchName, SubmissionNumber, MatterNumber);

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

    public static ConflictSearchListItem FromRow(ConflictSearchListRow row, ConflictSearchResultSummary? resultSummary)
    {
        return new ConflictSearchListItem
        {
            Id = row.Id,
            SearchNumber = row.SearchNumber,
            SearchName = row.SearchName,
            Status = row.Status,
            ReviewerDecision = row.ReviewerDecision,
            ResultCount = row.IsArchived ? row.ResultCount : resultSummary?.ResultCount ?? 0,
            HighestRiskLevel = row.IsArchived ? row.HighestRiskLevel : resultSummary?.HighestRiskLevel,
            RequestedBy = string.IsNullOrWhiteSpace(row.RequestedBy) ? "System" : row.RequestedBy,
            CreatedAt = row.CreatedAt,
            IsArchived = row.IsArchived,
            SubmissionNumber = row.SubmissionNumber,
            FormName = row.FormName,
            MatterNumber = row.MatterNumber,
            MatterName = row.MatterName
        };
    }
}
