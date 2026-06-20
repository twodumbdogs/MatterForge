using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Submissions;

public class IndexModel(
    CMIForgeDbContext db,
    CurrentUserService currentUserService,
    PermissionService permissionService) : PageModel
{
    public List<SubmissionListItem> Submissions { get; private set; } = [];

    public CMIForgeUser? CurrentUser { get; private set; }

    public bool CanViewAll { get; private set; }

    public int MySubmissionCount { get; private set; }

    public int AllSubmissionCount { get; private set; }

    public bool CanSubmitForms { get; private set; }

    public List<SubmissionFormOption> PublishedForms { get; private set; } = [];

    public RecordPage Pagination { get; private set; } = RecordPage.Empty;

    [BindProperty(SupportsGet = true)]
    public string View { get; set; } = "mine";

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SortColumn { get; set; } = "number";

    [BindProperty(SupportsGet = true)]
    public string SortDirection { get; set; } = ListSort.Descending;

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
            ? await db.FormSubmissions.AsNoTracking().CountAsync(x =>
                x.SubmitterUserId == currentUserId.Value &&
                x.Status != SubmissionStatuses.Cancelled)
            : 0;
        AllSubmissionCount = await db.FormSubmissions.AsNoTracking().CountAsync(x => x.Status != SubmissionStatuses.Cancelled);

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

        var submissionsQuery = db.FormSubmissions
            .AsNoTracking()
            .Where(x => x.Status != SubmissionStatuses.Cancelled);
        if (View == "mine")
        {
            submissionsQuery = currentUserId.HasValue
                ? submissionsQuery.Where(x => x.SubmitterUserId == currentUserId.Value)
                : submissionsQuery.Where(x => false);
        }

        Search = Search?.Trim();
        var recordNumber = SearchTermParser.TryParseRecordNumber(Search);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            submissionsQuery = submissionsQuery.Where(x =>
                (recordNumber.HasValue && x.SubmissionNumber == recordNumber.Value) ||
                x.Status.Contains(Search) ||
                x.SubmitterName.Contains(Search) ||
                x.DataJson.Contains(Search) ||
                (x.FormDefinition != null && x.FormDefinition.Name.Contains(Search)) ||
                (x.SubmitterUser != null && x.SubmitterUser.DisplayName.Contains(Search)) ||
                (x.Client != null && x.Client.Name.Contains(Search)) ||
                (x.Matter != null && x.Matter.Name.Contains(Search)));
        }

        SortColumn = NormalizeSortColumn(SortColumn);
        SortDirection = ListSort.NormalizeDirection(SortDirection);

        Pagination = RecordPage.Create(PageNumber, await submissionsQuery.CountAsync());

        var submissions = await ApplySort(submissionsQuery)
            .Include(x => x.Client)
            .Include(x => x.Matter)
            .Include(x => x.SubmitterUser)
            .Skip(Pagination.Skip)
            .Take(Pagination.PageSize)
            .ToListAsync();

        Submissions = submissions
            .Select(SubmissionListItem.FromSubmission)
            .ToList();

        return Page();
    }

    public string NextSortDirection(string column) => ListSort.NextDirection(SortColumn, SortDirection, column);

    public Dictionary<string, string> RouteValues => new()
    {
        ["View"] = View,
        ["Search"] = Search ?? string.Empty,
        ["SortColumn"] = SortColumn,
        ["SortDirection"] = SortDirection
    };

    private IQueryable<FormSubmission> ApplySort(IQueryable<FormSubmission> query)
    {
        var descending = ListSort.IsDescending(SortDirection);
        return SortColumn switch
        {
            "client" => descending ? query.OrderByDescending(x => x.Client!.Name).ThenByDescending(x => x.SubmissionNumber) : query.OrderBy(x => x.Client!.Name).ThenByDescending(x => x.SubmissionNumber),
            "matter" => descending ? query.OrderByDescending(x => x.Matter!.Name).ThenByDescending(x => x.SubmissionNumber) : query.OrderBy(x => x.Matter!.Name).ThenByDescending(x => x.SubmissionNumber),
            "submitter" => descending ? query.OrderByDescending(x => x.SubmitterUser!.DisplayName).ThenByDescending(x => x.SubmissionNumber) : query.OrderBy(x => x.SubmitterUser!.DisplayName).ThenByDescending(x => x.SubmissionNumber),
            "status" => descending ? query.OrderByDescending(x => x.Status).ThenByDescending(x => x.SubmissionNumber) : query.OrderBy(x => x.Status).ThenByDescending(x => x.SubmissionNumber),
            "submitted" => descending ? query.OrderByDescending(x => x.SubmittedAt).ThenByDescending(x => x.SubmissionNumber) : query.OrderBy(x => x.SubmittedAt).ThenByDescending(x => x.SubmissionNumber),
            _ => descending ? query.OrderByDescending(x => x.SubmissionNumber) : query.OrderBy(x => x.SubmissionNumber)
        };
    }

    private static string NormalizeSortColumn(string? column)
    {
        return column is "number" or "client" or "matter" or "submitter" or "status" or "submitted"
            ? column
            : "number";
    }
}

public sealed record SubmissionFormOption(Guid Id, string Name, int VersionNumber);

public sealed class SubmissionListItem
{
    public Guid Id { get; init; }

    public int SubmissionNumber { get; init; }

    public Guid? ClientId { get; init; }

    public string ClientName { get; init; } = string.Empty;

    public string SubmittedClientName { get; init; } = string.Empty;

    public Guid? MatterId { get; init; }

    public string MatterName { get; init; } = string.Empty;

    public string SubmittedMatterName { get; init; } = string.Empty;

    public Guid? SubmitterUserId { get; init; }

    public string SubmitterName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset SubmittedAt { get; init; }

    public static SubmissionListItem FromSubmission(FormSubmission submission)
    {
        var answers = SubmissionAnswerReader.Read(submission.DataJson);

        return new SubmissionListItem
        {
            Id = submission.Id,
            SubmissionNumber = submission.SubmissionNumber,
            ClientId = submission.ClientId,
            ClientName = submission.Client?.Name ?? string.Empty,
            SubmittedClientName = SubmissionAnswerReader.FirstValue(answers, "clientName", "client", "companyName"),
            MatterId = submission.MatterId,
            MatterName = submission.Matter?.Name ?? string.Empty,
            SubmittedMatterName = SubmissionAnswerReader.FirstValue(answers, "matterName", "matter"),
            SubmitterUserId = submission.SubmitterUserId,
            SubmitterName = submission.SubmitterUser?.DisplayName ?? submission.SubmitterName,
            Status = submission.Status,
            SubmittedAt = submission.SubmittedAt
        };
    }
}
