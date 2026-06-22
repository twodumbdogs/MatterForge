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
    PermissionService permissionService,
    AuditLogService auditLogService) : PageModel
{
    public ConflictSearch? Search { get; private set; }

    public ConflictSearchArchive? Archive { get; private set; }

    public List<ConflictResultDisplayItem> Results { get; private set; } = [];

    public bool IsArchived => Archive is not null;

    public bool CanReview { get; private set; }

    public bool CanRun { get; private set; }

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public List<AuditLog> AuditHistory { get; private set; } = [];

    public SelectList DecisionOptions { get; } = new(ConflictSearchDecisions.All);

    public IReadOnlyList<string> ResultStatusOptions { get; } = ConflictSearchDecisions.All;

    [BindProperty]
    public string Decision { get; set; } = ConflictSearchDecisions.Pending;

    [BindProperty]
    public string ReviewNotes { get; set; } = string.Empty;

    [BindProperty]
    public string AdditionalSearchTerms { get; set; } = string.Empty;

    [BindProperty]
    public Guid EscalatedToUserId { get; set; }

    [BindProperty]
    public string EscalationNotes { get; set; } = string.Empty;

    [BindProperty]
    public string EscalationApprovalNotes { get; set; } = string.Empty;

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

        var actionUser = await GetActionUserContextAsync();
        await conflictSearchService.ApplyReviewDecisionAsync(id, Decision, ReviewNotes, actionUser.ActorUserId, actionUser.ActingAsUserId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostResultReviewAsync(Guid id, Guid resultId, string resultStatus, string resultNotes)
    {
        if (!await permissionService.HasAsync(PermissionKeys.ConflictsReview))
        {
            return Forbid();
        }

        var actionUser = await GetActionUserContextAsync();
        await conflictSearchService.ApplyResultClearanceAsync(resultId, resultStatus, resultNotes, actionUser.ActorUserId, actionUser.ActingAsUserId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostBulkResultActionAsync(
        Guid id,
        List<Guid> selectedResultIds,
        string bulkResultAction,
        string bulkResultStatus,
        string bulkResultNotes)
    {
        if (!await permissionService.HasAsync(PermissionKeys.ConflictsReview))
        {
            return Forbid();
        }

        if (selectedResultIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Select at least one result first.");
            await LoadSearchAsync(id);
            return Page();
        }

        if (string.Equals(bulkResultAction, "escalate", StringComparison.OrdinalIgnoreCase))
        {
            var recipient = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == EscalatedToUserId && x.IsActive && !x.IsArchived);
            if (recipient is null)
            {
                ModelState.AddModelError(nameof(EscalatedToUserId), "Choose an active user to escalate to.");
                await LoadSearchAsync(id);
                return Page();
            }

            var actionUser = await GetActionUserContextAsync();
            var escalatedResults = await conflictSearchService.EscalateResultsAsync(selectedResultIds, EscalatedToUserId, EscalationNotes, actionUser.ActorUserId, actionUser.ActingAsUserId);
            await LogEscalationAsync(id, escalatedResults, recipient!, EscalationNotes, actionUser);
            return RedirectToPage(new { id });
        }

        if (!string.IsNullOrWhiteSpace(bulkResultAction) &&
            !string.Equals(bulkResultAction, "review", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Choose a valid bulk result action.");
            await LoadSearchAsync(id);
            return Page();
        }

        var bulkActionUser = await GetActionUserContextAsync();
        await conflictSearchService.ApplyResultClearanceAsync(selectedResultIds, bulkResultStatus, bulkResultNotes, bulkActionUser.ActorUserId, bulkActionUser.ActingAsUserId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostResultEscalateAsync(Guid id, Guid resultId)
    {
        if (!await permissionService.HasAsync(PermissionKeys.ConflictsReview))
        {
            return Forbid();
        }

        var recipient = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == EscalatedToUserId && x.IsActive && !x.IsArchived);
        if (recipient is null)
        {
            ModelState.AddModelError(nameof(EscalatedToUserId), "Choose an active user to escalate to.");
            await LoadSearchAsync(id);
            return Page();
        }

        var actionUser = await GetActionUserContextAsync();
        var escalatedResults = await conflictSearchService.EscalateResultsAsync([resultId], EscalatedToUserId, EscalationNotes, actionUser.ActorUserId, actionUser.ActingAsUserId);
        await LogEscalationAsync(id, escalatedResults, recipient, EscalationNotes, actionUser);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostApproveEscalationAsync(Guid id, Guid resultId)
    {
        if (!await permissionService.HasAsync(PermissionKeys.ConflictsReview))
        {
            return Forbid();
        }

        var actionUser = await GetActionUserContextAsync();
        if (actionUser.ActorUserId is null)
        {
            return Forbid();
        }

        var result = await conflictSearchService.ApproveEscalationAsync(resultId, EscalationApprovalNotes, actionUser.ActorUserId.Value, actionUser.ActingAsUserId);
        if (result is null)
        {
            ModelState.AddModelError(string.Empty, "Only the escalated reviewer can approve this result.");
            await LoadSearchAsync(id);
            return Page();
        }

        await auditLogService.LogAsync(
            "ConflictResult.EscalationApproved",
            "ConflictSearch",
            id,
            result.ConflictSearch is null ? null : RecordNumbers.ConflictSearch(result.ConflictSearch.SearchNumber),
            $"Escalation approved for result '{result.MatchedName}'.",
            new
            {
                ResultId = result.Id,
                result.MatchedName,
                result.SearchTerm,
                ApprovedByUserId = actionUser.ActorUserId,
                ApprovedAsUserId = actionUser.ActingAsUserId,
                Notes = EscalationApprovalNotes?.Trim() ?? string.Empty
            });

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
            .Include(x => x.Results)
                .ThenInclude(x => x.ClearedAsUser)
            .Include(x => x.Results)
                .ThenInclude(x => x.EscalatedToUser)
            .Include(x => x.Results)
                .ThenInclude(x => x.EscalatedByUser)
            .Include(x => x.Results)
                .ThenInclude(x => x.EscalatedAsUser)
            .Include(x => x.Results)
                .ThenInclude(x => x.EscalationApprovedByUser)
            .Include(x => x.Results)
                .ThenInclude(x => x.EscalationApprovedAsUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Search is not null)
        {
            UserOptions = await db.Users
                .AsNoTracking()
                .Where(x => x.IsActive && !x.IsArchived)
                .OrderBy(x => x.DisplayName)
                .ThenBy(x => x.Email)
                .Select(x => new SelectListItem(
                    !string.IsNullOrWhiteSpace(x.DisplayName) ? x.DisplayName : x.Email,
                    x.Id.ToString()))
                .ToListAsync();
            AuditHistory = await auditLogService.ListForEntityAsync("ConflictSearch", Search.Id, 12);
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

    private async Task LogEscalationAsync(Guid searchId, IReadOnlyCollection<ConflictSearchResult> results, CMIForgeUser recipient, string notes, ConflictActionUserContext actionUser)
    {
        if (results.Count == 0)
        {
            return;
        }

        var searchNumber = Search is null
            ? await db.ConflictSearches
                .AsNoTracking()
                .Where(x => x.Id == searchId)
                .Select(x => (int?)x.SearchNumber)
                .FirstOrDefaultAsync()
            : Search.SearchNumber;
        var resultSummaries = results
            .Select(x => new
            {
                x.Id,
                x.MatchedName,
                x.SearchTerm,
                x.RiskLevel,
                x.Score
            })
            .ToList();

        await auditLogService.LogAsync(
            "ConflictResult.Escalated",
            "ConflictSearch",
            searchId,
            searchNumber.HasValue ? RecordNumbers.ConflictSearch(searchNumber.Value) : null,
            $"{results.Count} conflict result(s) escalated to {recipient.DisplayName}.",
            new
            {
                EscalatedToUserId = recipient.Id,
                EscalatedToDisplayName = recipient.DisplayName,
                EscalatedByUserId = actionUser.ActorUserId,
                EscalatedAsUserId = actionUser.ActingAsUserId,
                Notes = notes?.Trim() ?? string.Empty,
                Results = resultSummaries
            });
    }

    private async Task<ConflictActionUserContext> GetActionUserContextAsync()
    {
        var actualUser = await currentUserService.GetActualCurrentUserAsync();
        var effectiveUser = await currentUserService.GetCurrentUserAsync();
        var actingAsUserId = actualUser is not null &&
            effectiveUser is not null &&
            actualUser.Id != effectiveUser.Id
                ? effectiveUser.Id
                : (Guid?)null;

        return new ConflictActionUserContext(actualUser?.Id, actingAsUserId);
    }
}

public sealed record ConflictActionUserContext(Guid? ActorUserId, Guid? ActingAsUserId);

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

    public string ClearedAsDisplayName { get; init; } = string.Empty;

    public string ClearanceActorDisplayName => FormatActionUserDisplayName(ClearedByDisplayName, ClearedAsDisplayName);

    public DateTimeOffset? ClearedAt { get; init; }

    public string EscalatedToDisplayName { get; init; } = string.Empty;

    public string EscalatedByDisplayName { get; init; } = string.Empty;

    public string EscalatedAsDisplayName { get; init; } = string.Empty;

    public string EscalationActorDisplayName => FormatActionUserDisplayName(EscalatedByDisplayName, EscalatedAsDisplayName);

    public DateTimeOffset? EscalatedAt { get; init; }

    public string EscalationNotes { get; init; } = string.Empty;

    public string EscalationApprovedByDisplayName { get; init; } = string.Empty;

    public string EscalationApprovedAsDisplayName { get; init; } = string.Empty;

    public string EscalationApprovalActorDisplayName => FormatActionUserDisplayName(EscalationApprovedByDisplayName, EscalationApprovedAsDisplayName);

    public DateTimeOffset? EscalationApprovedAt { get; init; }

    public string EscalationApprovalNotes { get; init; } = string.Empty;

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
            ClearedByDisplayName = result.ClearedByUser?.DisplayName ?? string.Empty,
            ClearedAsDisplayName = result.ClearedAsUser?.DisplayName ?? string.Empty,
            ClearedAt = result.ClearedAt,
            EscalatedToDisplayName = result.EscalatedToUser?.DisplayName ?? string.Empty,
            EscalatedByDisplayName = result.EscalatedByUser?.DisplayName ?? string.Empty,
            EscalatedAsDisplayName = result.EscalatedAsUser?.DisplayName ?? string.Empty,
            EscalatedAt = result.EscalatedAt,
            EscalationNotes = result.EscalationNotes,
            EscalationApprovedByDisplayName = result.EscalationApprovedByUser?.DisplayName ?? string.Empty,
            EscalationApprovedAsDisplayName = result.EscalationApprovedAsUser?.DisplayName ?? string.Empty,
            EscalationApprovedAt = result.EscalationApprovedAt,
            EscalationApprovalNotes = result.EscalationApprovalNotes
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
            ClearedAsDisplayName = result.ClearedAsDisplayName,
            ClearedAt = result.ClearedAt
        };
    }

    private static string FormatActionUserDisplayName(string actorDisplayName, string actingAsDisplayName)
    {
        var actor = string.IsNullOrWhiteSpace(actorDisplayName) ? "System" : actorDisplayName.Trim();
        var actingAs = string.IsNullOrWhiteSpace(actingAsDisplayName) ? string.Empty : actingAsDisplayName.Trim();
        return !string.IsNullOrWhiteSpace(actingAs) && !actor.Equals(actingAs, StringComparison.OrdinalIgnoreCase)
            ? $"{actor} impersonating {actingAs}"
            : actor;
    }
}
