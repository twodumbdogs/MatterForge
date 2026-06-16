using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Workflow.Definitions;

public partial class CreateModel(
    MatterForgeDbContext db,
    PermissionService permissionService,
    ProductPlanService productPlanService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public WorkflowDefinitionInput Input { get; set; } = WorkflowDefinitionInput.Default();

    public List<SelectListItem> FormOptions { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public List<SelectListItem> TeamOptions { get; private set; } = [];

    public List<SelectListItem> CompletionStatusOptions { get; } = SubmissionStatuses.All
        .Where(x => x != SubmissionStatuses.Converted)
        .Select(x => new SelectListItem(x, x))
        .ToList();

    public List<SelectListItem> ConditionOperatorOptions { get; } = WorkflowStepConditionOperators.All
        .Select(x => new SelectListItem(x, x))
        .ToList();

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

        await LoadOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!productPlanService.AllowsFeature(ProductFeatureKeys.Workflow))
        {
            return RedirectToPage("/Billing/Index", new { locked = ProductFeatureKeys.Workflow });
        }

        if (!await permissionService.HasAsync(PermissionKeys.WorkflowsDesign))
        {
            return Forbid();
        }

        await LoadOptionsAsync();
        await ValidateInputAsync(null);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var workflow = new WorkflowDefinition
        {
            Name = Input.Name.Trim(),
            Key = Input.Key.Trim(),
            Description = Input.Description?.Trim() ?? string.Empty,
            FormDefinitionId = Input.FormDefinitionId,
            IsActive = Input.IsActive
        };

        foreach (var step in UsedSteps())
        {
            workflow.Steps.Add(new WorkflowStep
            {
                StepNumber = step.StepNumber,
                Name = step.Name!.Trim(),
                Instructions = step.Instructions?.Trim() ?? string.Empty,
                AssignedUserId = step.AssignedUserId,
                AssignedTeamId = step.AssignedTeamId,
                ApprovalLabel = string.IsNullOrWhiteSpace(step.ApprovalLabel) ? "Approve" : step.ApprovalLabel.Trim(),
                CompletionSubmissionStatus = string.IsNullOrWhiteSpace(step.CompletionSubmissionStatus)
                    ? SubmissionStatuses.InReview
                    : step.CompletionSubmissionStatus,
                OutcomesJson = WorkflowOutcomeParser.Serialize(WorkflowOutcomeParser.FromDesignerText(
                    step.Outcomes,
                    step.ApprovalLabel,
                    step.CompletionSubmissionStatus)),
                ConditionFieldKey = step.ConditionFieldKey?.Trim() ?? string.Empty,
                ConditionOperator = string.IsNullOrWhiteSpace(step.ConditionOperator)
                    ? WorkflowStepConditionOperators.Always
                    : step.ConditionOperator,
                ConditionValue = step.ConditionValue?.Trim() ?? string.Empty
            });
        }

        db.WorkflowDefinitions.Add(workflow);
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Workflow.Created",
            "WorkflowDefinition",
            workflow.Id,
            workflow.Key,
            $"Created workflow {workflow.Name}.",
            new { StepCount = workflow.Steps.Count, workflow.IsActive });

        return RedirectToPage("./Index");
    }

    private async Task ValidateInputAsync(Guid? existingWorkflowId)
    {
        if (!string.IsNullOrWhiteSpace(Input.Key) && !SlugRegex().IsMatch(Input.Key))
        {
            ModelState.AddModelError("Input.Key", "Use lowercase letters, numbers, and hyphens only.");
        }

        if (!string.IsNullOrWhiteSpace(Input.Key))
        {
            var keyExists = await db.WorkflowDefinitions.AnyAsync(x => x.Id != existingWorkflowId && x.Key == Input.Key.Trim());
            if (keyExists)
            {
                ModelState.AddModelError("Input.Key", "Another workflow already uses this key.");
            }
        }

        var steps = UsedSteps().ToList();
        if (steps.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one workflow step.");
        }

        foreach (var step in steps)
        {
            if (step.StepNumber <= 0)
            {
                ModelState.AddModelError(string.Empty, "Workflow step numbers must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(step.Name))
            {
                ModelState.AddModelError(string.Empty, "Every used workflow step needs a name.");
            }

            if (!SubmissionStatuses.IsValid(step.CompletionSubmissionStatus))
            {
                ModelState.AddModelError(string.Empty, $"Invalid completion status for step {step.StepNumber}.");
            }

            if (!WorkflowStepConditionOperators.IsValid(step.ConditionOperator))
            {
                ModelState.AddModelError(string.Empty, $"Invalid routing condition for step {step.StepNumber}.");
            }

            var outcomes = WorkflowOutcomeParser.FromDesignerText(step.Outcomes, step.ApprovalLabel, step.CompletionSubmissionStatus);
            if (outcomes.Count == 0)
            {
                ModelState.AddModelError(string.Empty, $"Add at least one outcome for step {step.StepNumber}.");
            }
        }

        var duplicateStepNumbers = steps
            .GroupBy(x => x.StepNumber)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicateStepNumbers.Count > 0)
        {
            ModelState.AddModelError(string.Empty, $"Duplicate workflow step numbers: {string.Join(", ", duplicateStepNumbers)}.");
        }
    }

    private IEnumerable<WorkflowStepInput> UsedSteps()
    {
        return Input.Steps.Where(x => x.Id.HasValue || !string.IsNullOrWhiteSpace(x.Name));
    }

    private async Task LoadOptionsAsync()
    {
        FormOptions = await db.FormDefinitions
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();

        UserOptions = await db.Users
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem($"{x.DisplayName} ({x.SystemId:D8})", x.Id.ToString()))
            .ToListAsync();

        TeamOptions = await db.Teams
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();
}

public class WorkflowDefinitionInput
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Key { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Display(Name = "Default form scope")]
    public Guid? FormDefinitionId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public List<WorkflowStepInput> Steps { get; set; } = [];

    public static WorkflowDefinitionInput Default()
    {
        return new WorkflowDefinitionInput
        {
            Steps =
            [
                new() { StepNumber = 1, Name = "Review", ApprovalLabel = "Approve", CompletionSubmissionStatus = SubmissionStatuses.InReview },
                new() { StepNumber = 2, Name = "Final Approval", ApprovalLabel = "Approve", CompletionSubmissionStatus = SubmissionStatuses.Approved, Outcomes = "Approve|Complete|Approved; Return|Return|Returned" },
                new() { StepNumber = 3, ApprovalLabel = "Approve", CompletionSubmissionStatus = SubmissionStatuses.InReview }
            ]
        };
    }
}

public class WorkflowStepInput
{
    public Guid? Id { get; set; }

    public int StepNumber { get; set; }

    public string? Name { get; set; }

    public string? Instructions { get; set; }

    public Guid? AssignedUserId { get; set; }

    public Guid? AssignedTeamId { get; set; }

    public string? ApprovalLabel { get; set; } = "Approve";

    public string? CompletionSubmissionStatus { get; set; } = SubmissionStatuses.InReview;

    public string? Outcomes { get; set; }

    public string? ConditionFieldKey { get; set; }

    public string? ConditionOperator { get; set; } = WorkflowStepConditionOperators.Always;

    public string? ConditionValue { get; set; }
}
