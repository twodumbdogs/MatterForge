using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Forms;

public partial class CreateModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    ProductPlanService productPlanService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public CreateFormInput Input { get; set; } = CreateFormInput.Default();

    public SelectList FieldTypeOptions { get; } = new(Enum.GetValues<FieldType>());

    public List<SelectListItem> WorkflowOptions { get; private set; } = [];

    public int MaxFieldCount => FormDesignerLimits.MaxFields;

    public bool CanAttachWorkflow => productPlanService.AllowsFeature(ProductFeatureKeys.Workflow);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.FormsDesign))
        {
            return Forbid();
        }

        await LoadWorkflowOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.FormsDesign))
        {
            return Forbid();
        }

        await LoadWorkflowOptionsAsync();
        if (!CanAttachWorkflow)
        {
            Input.WorkflowDefinitionId = null;
        }

        var fieldRows = Input.Fields
            .Select((field, index) => new { Field = field, Index = index })
            .Where(x => !string.IsNullOrWhiteSpace(x.Field.Label) || !string.IsNullOrWhiteSpace(x.Field.Key))
            .ToList();

        var fields = fieldRows.Select(x => x.Field).ToList();

        if (fields.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one field before publishing the form.");
        }

        if (fields.Count > FormDesignerLimits.MaxFields)
        {
            ModelState.AddModelError(string.Empty, $"Forms can have up to {FormDesignerLimits.MaxFields} fields.");
        }

        if (!string.IsNullOrWhiteSpace(Input.Key) && !SlugRegex().IsMatch(Input.Key))
        {
            ModelState.AddModelError("Input.Key", "Use lowercase letters, numbers, and hyphens only.");
        }

        foreach (var row in fieldRows)
        {
            if (string.IsNullOrWhiteSpace(row.Field.Label))
            {
                ModelState.AddModelError($"Input.Fields[{row.Index}].Label", "Label is required when a field row is used.");
            }

            if (string.IsNullOrWhiteSpace(row.Field.Key))
            {
                ModelState.AddModelError($"Input.Fields[{row.Index}].Key", "Key is required when a field row is used.");
            }

            if (row.Field.Type == FieldType.Select && string.IsNullOrWhiteSpace(row.Field.Options))
            {
                ModelState.AddModelError($"Input.Fields[{row.Index}].Options", "Options are required for select fields.");
            }
        }

        var duplicateKeys = fields
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicateKeys.Count > 0)
        {
            ModelState.AddModelError(string.Empty, $"Duplicate field keys: {string.Join(", ", duplicateKeys)}.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var schema = new FormSchema
        {
            Title = Input.Name.Trim(),
            Sections = BuildSections(fields),
            Fields = fields.Select(x => BuildFormField(x)).ToList()
        };
        schema.Normalize();

        var form = new FormDefinition
        {
            Name = Input.Name.Trim(),
            Key = Input.Key.Trim(),
            Description = Input.Description?.Trim() ?? string.Empty,
            Versions =
            [
                new FormVersion
                {
                    VersionNumber = 1,
                    WorkflowDefinitionId = Input.WorkflowDefinitionId,
                    IsPublished = true,
                    PublishedAt = DateTimeOffset.UtcNow,
                    SchemaJson = JsonSerializer.Serialize(schema, FormJson.Options)
                }
            ]
        };

        db.FormDefinitions.Add(form);
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Form.Created",
            "FormDefinition",
            form.Id,
            form.Key,
            $"Created form {form.Name}.",
            new { FieldCount = schema.Fields.Count, WorkflowDefinitionId = Input.WorkflowDefinitionId });

        return RedirectToPage("./Submit", new { id = form.Id });
    }

    private static List<FormSection> BuildSections(IEnumerable<CreateFieldInput> fields)
    {
        return fields
            .Select(x => string.IsNullOrWhiteSpace(x.Section) ? "General" : x.Section.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => new FormSection
            {
                Key = FormSection.KeyFromLabel(x),
                Label = x
            })
            .ToList();
    }

    private static FormField BuildFormField(CreateFieldInput input)
    {
        var sectionLabel = string.IsNullOrWhiteSpace(input.Section) ? "General" : input.Section.Trim();
        return new FormField
        {
            SectionKey = FormSection.KeyFromLabel(sectionLabel),
            Label = input.Label!.Trim(),
            Key = input.Key!.Trim(),
            Type = input.Type,
            Required = input.Required,
            Options = (input.Options ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            VisibleWhenFieldKey = input.VisibleWhenFieldKey?.Trim() ?? string.Empty,
            VisibleWhenValue = input.VisibleWhenValue?.Trim() ?? string.Empty,
            EditableStepName = input.EditableStepName?.Trim() ?? string.Empty
        };
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();

    private async Task LoadWorkflowOptionsAsync()
    {
        if (!CanAttachWorkflow)
        {
            WorkflowOptions = [];
            return;
        }

        WorkflowOptions = await db.WorkflowDefinitions
            .Where(x => x.IsActive && x.IsPublished)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();
    }
}

public class CreateFormInput
{
    [Required]
    [Display(Name = "Form name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Form key")]
    public string Key { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Display(Name = "Workflow")]
    public Guid? WorkflowDefinitionId { get; set; }

    public List<CreateFieldInput> Fields { get; set; } = [];

    public static CreateFormInput Default()
    {
        return new CreateFormInput
        {
            Fields =
            [
                new() { Label = "Client name", Key = "clientName", Type = FieldType.Client, Required = true },
                new() { Section = "Matter", Label = "Matter name", Key = "matterName", Type = FieldType.Text, Required = true },
                new() { Section = "Matter", Label = "Practice area", Key = "practiceArea", Type = FieldType.Select, Required = true, Options = "Corporate, Litigation, Real Estate" },
                new() { Section = "Matter", Label = "Estimated fees", Key = "estimatedFees", Type = FieldType.Currency },
                new() { Section = "Review", Label = "Matter summary", Key = "summary", Type = FieldType.TextArea, Required = true },
                new(),
                new(),
                new()
            ]
        };
    }
}

public class CreateFieldInput
{
    public string? Section { get; set; } = "Client";

    public string? Label { get; set; }

    public string? Key { get; set; }

    public FieldType Type { get; set; } = FieldType.Text;

    public bool Required { get; set; }

    public string? Options { get; set; }

    public string? VisibleWhenFieldKey { get; set; }

    public string? VisibleWhenValue { get; set; }

    public string? EditableStepName { get; set; }
}
