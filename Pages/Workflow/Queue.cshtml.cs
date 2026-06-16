using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Workflow;

public class QueueModel(
    MatterForgeDbContext db,
    WorkflowService workflowService,
    CurrentUserService currentUserService,
    PermissionService permissionService,
    ProductPlanService productPlanService) : PageModel
{
    public List<SubmissionWorkflowTask> OpenTasks { get; private set; } = [];

    public List<SubmissionWorkflowEvent> RecentEvents { get; private set; } = [];

    public MatterForgeUser? CurrentUser { get; private set; }

    public bool CanViewAllQueues { get; private set; }

    public bool CanDesignWorkflows { get; private set; }

    public int MyTaskCount { get; private set; }

    public int TeamTaskCount { get; private set; }

    public int AllTaskCount { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string View { get; set; } = "mine";

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
        return RedirectToPage("./Queue", new { View });
    }

    private async Task LoadQueueAsync()
    {
        CurrentUser = await currentUserService.GetCurrentUserAsync();
        CanViewAllQueues = await permissionService.HasAsync(PermissionKeys.WorkflowsViewAllQueues);
        CanDesignWorkflows = await permissionService.HasAsync(PermissionKeys.WorkflowsDesign);
        var teamIds = await currentUserService.GetCurrentUserTeamIdsAsync();
        var currentUserId = CurrentUser?.Id;

        var openTaskBase = db.SubmissionWorkflowTasks
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

        OpenTasks = await tasksQuery
            .Include(x => x.FormSubmission)
                .ThenInclude(x => x!.FormDefinition)
            .Include(x => x.AssignedUser)
            .Include(x => x.AssignedTeam)
            .Include(x => x.WorkflowStep)
            .Include(x => x.SubmissionWorkflowInstance)
                .ThenInclude(x => x!.WorkflowDefinition)
            .Where(x => x.Status == WorkflowStatuses.TaskOpen)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        RecentEvents = await db.SubmissionWorkflowEvents
            .Include(x => x.FormSubmission)
                .ThenInclude(x => x!.FormDefinition)
            .Include(x => x.ActorUser)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync();
    }
}
