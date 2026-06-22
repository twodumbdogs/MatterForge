using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Workflow.Definitions;

public partial class CreateModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    ProductPlanService productPlanService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public WorkflowDefinitionInput Input { get; set; } = WorkflowDefinitionInput.Default();

    [BindProperty]
    public string WorkflowAction { get; set; } = "draft";

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

        await LoadOptionsAsync();
        ApplyDefaultNotificationTemplate();
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
        var publishRequested = IsPublishRequested();
        await ValidateInputAsync(null, publishRequested);

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
            IsActive = Input.IsActive,
            IsPublished = publishRequested,
            PublishedAt = publishRequested ? DateTimeOffset.UtcNow : null
        };

        foreach (var step in UsedSteps())
        {
            workflow.Steps.Add(new WorkflowStep
            {
                StepNumber = step.StepNumber,
                Name = step.Name!.Trim(),
                Instructions = step.Instructions?.Trim() ?? string.Empty,
                StepType = NormalizeStepType(step.StepType),
                AssignedUserId = step.AssignedUserId,
                AssignedTeamId = step.AssignedTeamId,
                ApprovalLabel = string.IsNullOrWhiteSpace(step.ApprovalLabel) ? "Approve" : step.ApprovalLabel.Trim(),
                CompletionSubmissionStatus = string.IsNullOrWhiteSpace(step.CompletionSubmissionStatus)
                    ? SubmissionStatuses.InReview
                    : step.CompletionSubmissionStatus,
                OutcomesJson = IsNotificationStep(step)
                    ? "[]"
                    : WorkflowOutcomeParser.Serialize(WorkflowOutcomeParser.FromDesignerText(
                        step.Outcomes,
                        step.ApprovalLabel,
                        step.CompletionSubmissionStatus)),
                ConditionFieldKey = step.ConditionFieldKey?.Trim() ?? string.Empty,
                ConditionOperator = string.IsNullOrWhiteSpace(step.ConditionOperator)
                    ? WorkflowStepConditionOperators.Always
                    : step.ConditionOperator,
                ConditionValue = step.ConditionValue?.Trim() ?? string.Empty,
                NotificationSubject = step.NotificationSubject?.Trim() ?? string.Empty,
                NotificationBody = step.NotificationBody?.Trim() ?? string.Empty,
                NotificationRecipients = WorkflowNotificationRecipientInput.Normalize(step.NotificationRecipientTokens),
                NotificationTemplateId = IsNotificationStep(step) ? step.NotificationTemplateId : null
            });
        }

        db.WorkflowDefinitions.Add(workflow);
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Workflow.Created",
            "WorkflowDefinition",
            workflow.Id,
            workflow.Key,
            publishRequested ? $"Created and published workflow {workflow.Name}." : $"Saved draft workflow {workflow.Name}.",
            new { StepCount = workflow.Steps.Count, workflow.IsActive, workflow.IsPublished });

        return RedirectToPage("./Index");
    }

    private async Task ValidateInputAsync(Guid? existingWorkflowId, bool requirePublishReady)
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

    private void ApplyDefaultNotificationTemplate()
    {
        var defaultTemplateId = NotificationTemplateOptions.FirstOrDefault()?.Value;
        if (!Guid.TryParse(defaultTemplateId, out var templateId))
        {
            return;
        }

        foreach (var step in Input.Steps.Where(IsNotificationStep))
        {
            step.NotificationTemplateId ??= templateId;
        }
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
                new() { StepNumber = 2, Name = "Notify Intake Team", StepType = WorkflowStepTypes.Notification, NotificationRecipientTokens = [WorkflowNotificationRecipientTokens.Assigned, WorkflowNotificationRecipientTokens.Submitter], NotificationSubject = "Submission {{SubmissionNumber}} is moving", NotificationBody = "{{WorkflowName}} reached {{StepName}} for {{SubmissionNumber}}." },
                new() { StepNumber = 3, Name = "Final Approval", ApprovalLabel = "Approve", CompletionSubmissionStatus = SubmissionStatuses.Approved, Outcomes = "Approve|Complete|Approved; Return|Return|Returned" },
                new() { StepNumber = 4, ApprovalLabel = "Approve", CompletionSubmissionStatus = SubmissionStatuses.InReview }
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

    public string? StepType { get; set; } = WorkflowStepTypes.Approval;

    public Guid? AssignedUserId { get; set; }

    public Guid? AssignedTeamId { get; set; }

    public string? ApprovalLabel { get; set; } = "Approve";

    public string? CompletionSubmissionStatus { get; set; } = SubmissionStatuses.InReview;

    public string? Outcomes { get; set; }

    public string? ConditionFieldKey { get; set; }

    public string? ConditionOperator { get; set; } = WorkflowStepConditionOperators.Always;

    public string? ConditionValue { get; set; }

    public string? NotificationSubject { get; set; }

    public string? NotificationBody { get; set; }

    public string? NotificationRecipients { get; set; }

    public List<string> NotificationRecipientTokens { get; set; } = [];

    public Guid? NotificationTemplateId { get; set; }
}

public static class WorkflowNotificationRecipientInput
{
    private static readonly SelectListGroup WorkflowGroup = new() { Name = "Workflow" };
    private static readonly SelectListGroup UsersGroup = new() { Name = "Users" };
    private static readonly SelectListGroup ContactsGroup = new() { Name = "Contacts" };

    public static async Task<List<SelectListItem>> LoadOptionsAsync(CMIForgeDbContext db)
    {
        var options = new List<SelectListItem>
        {
            new("Assigned user/team", WorkflowNotificationRecipientTokens.Assigned) { Group = WorkflowGroup },
            new("Submitter", WorkflowNotificationRecipientTokens.Submitter) { Group = WorkflowGroup }
        };

        var users = await db.Users
            .Where(x => x.IsActive && !x.IsArchived && x.Email != string.Empty)
            .OrderBy(x => x.DisplayName)
            .Select(x => new { x.Id, x.DisplayName, x.Email })
            .ToListAsync();
        options.AddRange(users.Select(x => new SelectListItem(
            $"{x.DisplayName} ({x.Email})",
            WorkflowNotificationRecipientTokens.User(x.Id))
        {
            Group = UsersGroup
        }));

        var contacts = await db.Contacts
            .Where(x => !x.IsArchived && x.Email != string.Empty)
            .OrderBy(x => x.DisplayName)
            .Select(x => new { x.Id, x.DisplayName, x.Email })
            .ToListAsync();
        options.AddRange(contacts.Select(x => new SelectListItem(
            $"{x.DisplayName} ({x.Email})",
            WorkflowNotificationRecipientTokens.Contact(x.Id))
        {
            Group = ContactsGroup
        }));

        return options;
    }

    public static string Normalize(IEnumerable<string>? tokens)
    {
        return string.Join(';', (tokens ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    public static List<string> Parse(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split([';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
    }
}
