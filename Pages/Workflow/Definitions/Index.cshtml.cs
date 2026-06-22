using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Workflow.Definitions;

public class IndexModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    ProductPlanService productPlanService,
    AuditLogService auditLogService) : PageModel
{
    public List<WorkflowDefinition> Workflows { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!productPlanService.AllowsFeature(ProductFeatureKeys.Workflow))
        {
            return RedirectToPage("/Billing/Index", new { locked = ProductFeatureKeys.Workflow });
        }

        if (!await permissionService.HasAsync(PermissionKeys.WorkflowsDesign))
        {
            return Forbid();
        }

        Workflows = await db.WorkflowDefinitions
            .Include(x => x.FormDefinition)
            .Include(x => x.Steps)
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostCopyAsync(Guid id)
    {
        if (!productPlanService.AllowsFeature(ProductFeatureKeys.Workflow))
        {
            return RedirectToPage("/Billing/Index", new { locked = ProductFeatureKeys.Workflow });
        }

        if (!await permissionService.HasAsync(PermissionKeys.WorkflowsDesign))
        {
            return Forbid();
        }

        var source = await db.WorkflowDefinitions
            .Include(x => x.Steps)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (source is null)
        {
            return NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var copy = new WorkflowDefinition
        {
            Name = CopyName(source.Name),
            Key = await CopyKeyAsync(source.Key),
            Description = source.Description,
            FormDefinitionId = source.FormDefinitionId,
            IsActive = source.IsActive,
            IsPublished = false,
            PublishedAt = null,
            CreatedAt = now,
            UpdatedAt = now,
            Steps = source.Steps
                .OrderBy(x => x.StepNumber)
                .Select(x => new WorkflowStep
                {
                    StepNumber = x.StepNumber,
                    Name = x.Name,
                    Instructions = x.Instructions,
                    StepType = x.StepType,
                    AssignedUserId = x.AssignedUserId,
                    AssignedTeamId = x.AssignedTeamId,
                    ApprovalLabel = x.ApprovalLabel,
                    CompletionSubmissionStatus = x.CompletionSubmissionStatus,
                    OutcomesJson = x.OutcomesJson,
                    ConditionFieldKey = x.ConditionFieldKey,
                    ConditionOperator = x.ConditionOperator,
                    ConditionValue = x.ConditionValue,
                    NotificationSubject = x.NotificationSubject,
                    NotificationBody = x.NotificationBody,
                    NotificationRecipients = x.NotificationRecipients,
                    NotificationTemplateId = x.NotificationTemplateId,
                    CreatedAt = now,
                    UpdatedAt = now
                })
                .ToList()
        };

        db.WorkflowDefinitions.Add(copy);
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Workflow.Copied",
            "WorkflowDefinition",
            copy.Id,
            copy.Key,
            $"Copied workflow {source.Name} to draft {copy.Name}.",
            new { SourceWorkflowDefinitionId = source.Id, StepCount = copy.Steps.Count });

        return RedirectToPage("./Edit", new { id = copy.Id });
    }

    private static string CopyName(string sourceName)
    {
        var name = $"Copy of {sourceName}".Trim();
        return name.Length <= 160 ? name : name[..160];
    }

    private async Task<string> CopyKeyAsync(string sourceKey)
    {
        var root = string.IsNullOrWhiteSpace(sourceKey) ? "workflow" : sourceKey.Trim();
        root = root.Length > 68 ? root[..68].TrimEnd('-') : root;
        var candidate = $"{root}-copy";
        var index = 2;

        while (await db.WorkflowDefinitions.AnyAsync(x => x.Key == candidate))
        {
            var suffix = $"-copy-{index++}";
            var maxRootLength = 80 - suffix.Length;
            var trimmedRoot = root.Length > maxRootLength ? root[..maxRootLength].TrimEnd('-') : root;
            candidate = $"{trimmedRoot}{suffix}";
        }

        return candidate;
    }
}
