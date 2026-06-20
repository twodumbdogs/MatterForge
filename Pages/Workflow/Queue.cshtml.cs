using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Workflow;

public class QueueModel(
    CMIForgeDbContext db,
    WorkflowService workflowService,
    CurrentUserService currentUserService,
    PermissionService permissionService,
    ProductPlanService productPlanService) : PageModel
{
    public List<SubmissionWorkflowTask> OpenTasks { get; private set; } = [];

    public List<SubmissionWorkflowEvent> RecentEvents { get; private set; } = [];

    public CMIForgeUser? CurrentUser { get; private set; }

    public bool CanViewAllQueues { get; private set; }

    public bool CanDesignWorkflows { get; private set; }

    public int MyTaskCount { get; private set; }

    public int TeamTaskCount { get; private set; }

    public int AllTaskCount { get; private set; }

    public RecordPage Pagination { get; private set; } = RecordPage.Empty;

    [BindProperty(SupportsGet = true)]
    public string View { get; set; } = "mine";

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SortColumn { get; set; } = "started";

    [BindProperty(SupportsGet = true)]
    public string SortDirection { get; set; } = ListSort.Ascending;

    [BindProperty]
    public string Notes { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!productPlanService.AllowsFeature(ProductFeatureKeys.Workflow))
        {
            return RedirectToPage("/Billing/Index", new { locked = ProductFeatureKeys.Workflow });
        }

        if (!await permissionService.HasAsync(PermissionKeys.WorkflowsViewQueue))
        {
            return Forbid();
        }

        await LoadQueueAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostOutcomeAsync(Guid taskId, string outcomeKey)
    {
        if (!productPlanService.AllowsFeature(ProductFeatureKeys.Workflow))
        {
            return RedirectToPage("/Billing/Index", new { locked = ProductFeatureKeys.Workflow });
        }

        var task = await db.SubmissionWorkflowTasks.FirstOrDefaultAsync(x => x.Id == taskId);
        if (task is null || !await permissionService.CanActOnTaskAsync(task))
        {
            return Forbid();
        }

        await workflowService.ApplyOutcomeAsync(taskId, outcomeKey, Notes, (await currentUserService.GetCurrentUserAsync())?.Id);
        return RedirectToPage("./Queue", new { View, Search });
    }

    private async Task LoadQueueAsync()
    {
        CurrentUser = await currentUserService.GetCurrentUserAsync();
        CanViewAllQueues = await permissionService.HasAsync(PermissionKeys.WorkflowsViewAllQueues);
        CanDesignWorkflows = await permissionService.HasAsync(PermissionKeys.WorkflowsDesign);
        var teamIds = await currentUserService.GetCurrentUserTeamIdsAsync();
        var currentUserId = CurrentUser?.Id;

        var openTaskBase = db.SubmissionWorkflowTasks
            .AsNoTracking()
            .Where(x => x.Status == WorkflowStatuses.TaskOpen);

        MyTaskCount = currentUserId.HasValue
            ? await openTaskBase.CountAsync(x => x.AssignedUserId == currentUserId.Value)
            : 0;

        TeamTaskCount = teamIds.Count > 0
            ? await openTaskBase.CountAsync(x => x.AssignedTeamId.HasValue && teamIds.Contains(x.AssignedTeamId.Value))
            : 0;

        AllTaskCount = await openTaskBase.CountAsync();

        if (View is not ("mine" or "team" or "all"))
        {
            View = "mine";
        }

        if (View == "all" && !CanViewAllQueues)
        {
            View = "mine";
        }

        var tasksQuery = openTaskBase;
        if (View == "mine")
        {
            tasksQuery = currentUserId.HasValue
                ? tasksQuery.Where(x => x.AssignedUserId == currentUserId.Value)
                : tasksQuery.Where(x => false);
        }
        else if (View == "team")
        {
            tasksQuery = teamIds.Count > 0
                ? tasksQuery.Where(x => x.AssignedTeamId.HasValue && teamIds.Contains(x.AssignedTeamId.Value))
                : tasksQuery.Where(x => false);
        }

        Search = Search?.Trim();
        var recordNumber = SearchTermParser.TryParseRecordNumber(Search);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            tasksQuery = tasksQuery.Where(x =>
                (recordNumber.HasValue && x.FormSubmission != null && x.FormSubmission.SubmissionNumber == recordNumber.Value) ||
                x.Status.Contains(Search) ||
                x.Outcome.Contains(Search) ||
                x.Notes.Contains(Search) ||
                (x.FormSubmission != null && x.FormSubmission.SubmitterName.Contains(Search)) ||
                (x.FormSubmission != null && x.FormSubmission.DataJson.Contains(Search)) ||
                (x.FormSubmission != null && x.FormSubmission.FormDefinition != null && x.FormSubmission.FormDefinition.Name.Contains(Search)) ||
                (x.AssignedUser != null && x.AssignedUser.DisplayName.Contains(Search)) ||
                (x.AssignedTeam != null && x.AssignedTeam.Name.Contains(Search)) ||
                (x.WorkflowStep != null && x.WorkflowStep.Name.Contains(Search)) ||
                (x.WorkflowStep != null && x.WorkflowStep.Instructions.Contains(Search)) ||
                (x.SubmissionWorkflowInstance != null &&
                    x.SubmissionWorkflowInstance.WorkflowDefinition != null &&
                    x.SubmissionWorkflowInstance.WorkflowDefinition.Name.Contains(Search)));
        }

        SortColumn = NormalizeSortColumn(SortColumn);
        SortDirection = ListSort.NormalizeDirection(SortDirection);

        Pagination = RecordPage.Create(PageNumber, await tasksQuery.CountAsync());

        OpenTasks = await ApplySort(tasksQuery)
            .Include(x => x.FormSubmission)
                .ThenInclude(x => x!.FormDefinition)
            .Include(x => x.AssignedUser)
            .Include(x => x.AssignedTeam)
            .Include(x => x.WorkflowStep)
            .Include(x => x.SubmissionWorkflowInstance)
                .ThenInclude(x => x!.WorkflowDefinition)
            .Skip(Pagination.Skip)
            .Take(Pagination.PageSize)
            .ToListAsync();

        RecentEvents = await db.SubmissionWorkflowEvents
            .AsNoTracking()
            .Include(x => x.FormSubmission)
                .ThenInclude(x => x!.FormDefinition)
            .Include(x => x.ActorUser)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync();
    }

    public string NextSortDirection(string column) => ListSort.NextDirection(SortColumn, SortDirection, column);

    public Dictionary<string, string> RouteValues => new()
    {
        ["View"] = View,
        ["Search"] = Search ?? string.Empty,
        ["SortColumn"] = SortColumn,
        ["SortDirection"] = SortDirection
    };

    private IQueryable<SubmissionWorkflowTask> ApplySort(IQueryable<SubmissionWorkflowTask> query)
    {
        var descending = ListSort.IsDescending(SortDirection);
        return SortColumn switch
        {
            "submission" => descending ? query.OrderByDescending(x => x.FormSubmission!.SubmissionNumber).ThenBy(x => x.CreatedAt) : query.OrderBy(x => x.FormSubmission!.SubmissionNumber).ThenBy(x => x.CreatedAt),
            "workflow" => descending ? query.OrderByDescending(x => x.SubmissionWorkflowInstance!.WorkflowDefinition!.Name).ThenBy(x => x.CreatedAt) : query.OrderBy(x => x.SubmissionWorkflowInstance!.WorkflowDefinition!.Name).ThenBy(x => x.CreatedAt),
            "step" => descending ? query.OrderByDescending(x => x.WorkflowStep!.Name).ThenBy(x => x.CreatedAt) : query.OrderBy(x => x.WorkflowStep!.Name).ThenBy(x => x.CreatedAt),
            "assigned" => descending ? query.OrderByDescending(x => x.AssignedUser!.DisplayName ?? x.AssignedTeam!.Name).ThenBy(x => x.CreatedAt) : query.OrderBy(x => x.AssignedUser!.DisplayName ?? x.AssignedTeam!.Name).ThenBy(x => x.CreatedAt),
            "status" => descending ? query.OrderByDescending(x => x.Status).ThenBy(x => x.CreatedAt) : query.OrderBy(x => x.Status).ThenBy(x => x.CreatedAt),
            _ => descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt)
        };
    }

    private static string NormalizeSortColumn(string? column)
    {
        return column is "submission" or "workflow" or "step" or "assigned" or "status" or "started"
            ? column
            : "started";
    }
}
