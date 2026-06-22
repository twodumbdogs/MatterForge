using System.Text.RegularExpressions;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Workflow.Definitions;

public partial class EditModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    ProductPlanService productPlanService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public WorkflowDefinitionInput Input { get; set; } = new();

    [BindProperty]
    public string WorkflowAction { get; set; } = "draft";

    public bool IsPublished { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public List<SelectListItem> FormOptions { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public List<SelectListItem> TeamOptions { get; private set; } = [];

    public List<SelectListItem> NotificationRecipientOptions { get; private set; } = [];

    public List<SelectListItem> NotificationTemplateOptions { get; private set; } = [];

    public List<SelectListItem> StepTypeOptions { get; } = WorkflowStepTypes.All
        .Select(x => new SelectListItem(x, x))
        .ToList();

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

        var workflow = await db.WorkflowDefinitions
            .Include(x => x.Steps)
            .FirstOrDefaultAsync(x => x.Id == Id);

        if (workflow is null)
        {
            return NotFound();
        }

        IsPublished = workflow.IsPublished;
        PublishedAt = workflow.PublishedAt;
        Input = new WorkflowDefinitionInput
        {
            Name = workflow.Name,
            Key = workflow.Key,
            Description = workflow.Description,
            FormDefinitionId = workflow.FormDefinitionId,
            IsActive = workflow.IsActive,
            Steps = workflow.Steps
                .OrderBy(x => x.StepNumber)
                .Select(x => new WorkflowStepInput
                {
                    Id = x.Id,
                    StepNumber = x.StepNumber,
                    Name = x.Name,
                    Instructions = x.Instructions,
                    StepType = x.StepType,
                    AssignedUserId = x.AssignedUserId,
                    AssignedTeamId = x.AssignedTeamId,
                    ApprovalLabel = x.ApprovalLabel,
                    CompletionSubmissionStatus = x.CompletionSubmissionStatus,
                    Outcomes = WorkflowOutcomeParser.ToDesignerText(x),
                    ConditionFieldKey = x.ConditionFieldKey,
                    ConditionOperator = x.ConditionOperator,
                    ConditionValue = x.ConditionValue,
                    NotificationSubject = x.NotificationSubject,
                    NotificationBody = x.NotificationBody,
                    NotificationRecipients = x.NotificationRecipients,
                    NotificationRecipientTokens = WorkflowNotificationRecipientInput.Parse(x.NotificationRecipients),
                    NotificationTemplateId = x.NotificationTemplateId
                })
                .ToList()
        };

        var nextStepNumber = Input.Steps.Count == 0 ? 1 : Input.Steps.Max(x => x.StepNumber) + 1;
        Input.Steps.Add(new WorkflowStepInput { StepNumber = nextStepNumber, ApprovalLabel = "Approve", CompletionSubmissionStatus = SubmissionStatuses.InReview });
        Input.Steps.Add(new WorkflowStepInput { StepNumber = nextStepNumber + 1, ApprovalLabel = "Approve", CompletionSubmissionStatus = SubmissionStatuses.InReview });

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

        var workflow = await db.WorkflowDefinitions
            .Include(x => x.Steps)
            .FirstOrDefaultAsync(x => x.Id == Id);

        if (workflow is null)
        {
            return NotFound();
        }

        var publishRequested = IsPublishRequested();
        await ValidateInputAsync(Id, publishRequested);
        if (!ModelState.IsValid)
        {
            IsPublished = workflow.IsPublished;
            PublishedAt = workflow.PublishedAt;
            return Page();
        }

        workflow.Name = Input.Name.Trim();
        workflow.Key = Input.Key.Trim();
        workflow.Description = Input.Description?.Trim() ?? string.Empty;
        workflow.FormDefinitionId = Input.FormDefinitionId;
        workflow.IsActive = Input.IsActive;
        workflow.IsPublished = publishRequested;
        workflow.PublishedAt = publishRequested ? DateTimeOffset.UtcNow : null;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;

        var usedSteps = UsedSteps().ToList();
        var existingUsedStepIds = usedSteps
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        var tempStepNumber = -1;
        foreach (var existingStep in workflow.Steps.Where(x => existingUsedStepIds.Contains(x.Id)))
        {
            existingStep.StepNumber = tempStepNumber--;
        }

        if (existingUsedStepIds.Count > 0)
        {
            await db.SaveChangesAsync();
        }

        foreach (var stepInput in usedSteps)
        {
            var step = stepInput.Id.HasValue
                ? workflow.Steps.FirstOrDefault(x => x.Id == stepInput.Id.Value)
                : null;

            if (step is null)
            {
                step = new WorkflowStep();
                workflow.Steps.Add(step);
            }

            step.StepNumber = stepInput.StepNumber;
            step.Name = stepInput.Name!.Trim();
            step.Instructions = stepInput.Instructions?.Trim() ?? string.Empty;
            step.StepType = NormalizeStepType(stepInput.StepType);
            step.AssignedUserId = stepInput.AssignedUserId;
            step.AssignedTeamId = stepInput.AssignedTeamId;
            step.ApprovalLabel = string.IsNullOrWhiteSpace(stepInput.ApprovalLabel) ? "Approve" : stepInput.ApprovalLabel.Trim();
            step.CompletionSubmissionStatus = string.IsNullOrWhiteSpace(stepInput.CompletionSubmissionStatus)
                ? SubmissionStatuses.InReview
                : stepInput.CompletionSubmissionStatus;
            step.OutcomesJson = IsNotificationStep(stepInput)
                ? "[]"
                : WorkflowOutcomeParser.Serialize(WorkflowOutcomeParser.FromDesignerText(
                    stepInput.Outcomes,
                    stepInput.ApprovalLabel,
                    stepInput.CompletionSubmissionStatus));
            step.ConditionFieldKey = stepInput.ConditionFieldKey?.Trim() ?? string.Empty;
            step.ConditionOperator = string.IsNullOrWhiteSpace(stepInput.ConditionOperator)
                ? WorkflowStepConditionOperators.Always
                : stepInput.ConditionOperator;
            step.ConditionValue = stepInput.ConditionValue?.Trim() ?? string.Empty;
            step.NotificationSubject = stepInput.NotificationSubject?.Trim() ?? string.Empty;
            step.NotificationBody = stepInput.NotificationBody?.Trim() ?? string.Empty;
            step.NotificationRecipients = WorkflowNotificationRecipientInput.Normalize(stepInput.NotificationRecipientTokens);
            step.NotificationTemplateId = IsNotificationStep(stepInput) ? stepInput.NotificationTemplateId : null;
            step.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Workflow.Updated",
            "WorkflowDefinition",
            workflow.Id,
            workflow.Key,
            publishRequested ? $"Published workflow {workflow.Name}." : $"Saved draft workflow {workflow.Name}.",
            new { StepCount = workflow.Steps.Count, workflow.IsActive, workflow.IsPublished });

        return RedirectToPage("./Index");
    }

    private async Task ValidateInputAsync(Guid existingWorkflowId, bool requirePublishReady)
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
        if (requirePublishReady && steps.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one workflow step before publishing.");
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

            if (!WorkflowStepTypes.IsValid(step.StepType))
            {
                ModelState.AddModelError(string.Empty, $"Invalid step type for step {step.StepNumber}.");
            }

            if (!SubmissionStatuses.IsValid(step.CompletionSubmissionStatus))
            {
                ModelState.AddModelError(string.Empty, $"Invalid completion status for step {step.StepNumber}.");
            }

            if (!WorkflowStepConditionOperators.IsValid(step.ConditionOperator))
            {
                ModelState.AddModelError(string.Empty, $"Invalid routing condition for step {step.StepNumber}.");
            }

            if (IsNotificationStep(step))
            {
                ValidateNotificationRecipients(step, requirePublishReady);
            }
            else
            {
                var outcomes = WorkflowOutcomeParser.FromDesignerText(step.Outcomes, step.ApprovalLabel, step.CompletionSubmissionStatus);
                if (requirePublishReady && outcomes.Count == 0)
                {
                    ModelState.AddModelError(string.Empty, $"Add at least one outcome for step {step.StepNumber}.");
                }
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

    private static bool IsNotificationStep(WorkflowStepInput step)
    {
        return NormalizeStepType(step.StepType).Equals(WorkflowStepTypes.Notification, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsPublishRequested()
    {
        return WorkflowAction.Equals("publish", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeStepType(string? stepType)
    {
        return WorkflowStepTypes.IsValid(stepType) ? stepType! : WorkflowStepTypes.Approval;
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

        NotificationRecipientOptions = await WorkflowNotificationRecipientInput.LoadOptionsAsync(db);

        NotificationTemplateOptions = await db.WorkflowNotificationTemplates
            .Where(x => x.IsActive)
            .OrderBy(x => x.Key == "workflow-step-update" ? 0 : 1)
            .ThenBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();
    }

    private void ValidateNotificationRecipients(WorkflowStepInput step, bool requireRecipients)
    {
        var selected = step.NotificationRecipientTokens
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        if (selected.Count == 0)
        {
            if (requireRecipients)
            {
                ModelState.AddModelError(string.Empty, $"Select at least one notification recipient for step {step.StepNumber} before publishing.");
            }

            return;
        }

        var validTokens = NotificationRecipientOptions
            .Select(x => x.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var token in selected)
        {
            if (!validTokens.Contains(token))
            {
                ModelState.AddModelError(string.Empty, $"Notification step {step.StepNumber} has an invalid recipient selection.");
                return;
            }
        }

        if (step.NotificationTemplateId.HasValue && NotificationTemplateOptions.All(x => x.Value != step.NotificationTemplateId.Value.ToString()))
        {
            ModelState.AddModelError(string.Empty, $"Notification step {step.StepNumber} has an invalid notification template.");
        }
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();
}
