using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Forms;

public partial class EditModel(
    MatterForgeDbContext db,
    PermissionService permissionService,
    ProductPlanService productPlanService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public EditFormInput Input { get; set; } = new();

    public SelectList FieldTypeOptions { get; } = new(Enum.GetValues<FieldType>());

    public List<SelectListItem> WorkflowOptions { get; private set; } = [];

    public int CurrentVersionNumber { get; private set; }

    public bool CanAttachWorkflow => productPlanService.AllowsFeature(ProductFeatureKeys.Workflow);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.FormsDesign))
        {
            return Forbid();
        }

        var form = await db.FormDefinitions
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == Id && x.IsActive);

        if (form is null)
        {
            return NotFound();
        }

        var latestVersion = form.Versions
            .Where(x => x.IsPublished)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefault();

        if (latestVersion is null)
        {
            return NotFound();
        }

        var schema = FormJson.DeserializeSchema(latestVersion.SchemaJson);
        CurrentVersionNumber = latestVersion.VersionNumber;
        Input = new EditFormInput
        {
            Name = form.Name,
            Key = form.Key,
            Description = form.Description,
            WorkflowDefinitionId = latestVersion.WorkflowDefinitionId,
            Fields = schema.Fields
                .Select(x => new EditFieldInput
                {
                    Label = x.Label,
                    Key = x.Key,
                    Type = x.Type,
                    Required = x.Required,
                    Options = string.Join(", ", x.Options)
                })
                .Concat([new EditFieldInput(), new EditFieldInput(), new EditFieldInput()])
                .ToList()
        };

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

        var form = await db.FormDefinitions
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == Id && x.IsActive);

        if (form is null)
        {
            return NotFound();
        }

        var fieldRows = Input.Fields
            .Select((field, index) => new { Field = field, Index = index })
            .Where(x => !string.IsNullOrWhiteSpace(x.Field.Label) || !string.IsNullOrWhiteSpace(x.Field.Key))
            .ToList();

        var fields = fieldRows.Select(x => x.Field).ToList();

        if (fields.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one field before publishing a new version.");
        }

        if (!string.IsNullOrWhiteSpace(Input.Key) && !SlugRegex().IsMatch(Input.Key))
        {
            ModelState.AddModelError("Input.Key", "Use lowercase letters, numbers, and hyphens only.");
        }

        var keyExists = await db.FormDefinitions.AnyAsync(x => x.Id != Id && x.Key == Input.Key.Trim());
        if (keyExists)
        {
            ModelState.AddModelError("Input.Key", "Another form already uses this key.");
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
            CurrentVersionNumber = form.Versions.Max(x => x.VersionNumber);
            return Page();
        }

        var schema = new FormSchema
        {
            Title = Input.Name.Trim(),
            Fields = fields.Select(x => new FormField
            {
                Label = x.Label!.Trim(),
                Key = x.Key!.Trim(),
                Type = x.Type,
                Required = x.Required,
                Options = (x.Options ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList()
            }).ToList()
        };

        form.Name = Input.Name.Trim();
        form.Key = Input.Key.Trim();
        form.Description = Input.Description?.Trim() ?? string.Empty;
        form.UpdatedAt = DateTimeOffset.UtcNow;

        var nextVersion = form.Versions.Count == 0 ? 1 : form.Versions.Max(x => x.VersionNumber) + 1;
        form.Versions.Add(new FormVersion
        {
            VersionNumber = nextVersion,
            WorkflowDefinitionId = CanAttachWorkflow ? Input.WorkflowDefinitionId : null,
            IsPublished = true,
            PublishedAt = DateTimeOffset.UtcNow,
            SchemaJson = JsonSerializer.Serialize(schema, FormJson.Options)
        });

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Form.VersionPublished",
            "FormDefinition",
            form.Id,
            form.Key,
            $"Published version {nextVersion} for form {form.Name}.",
            new { VersionNumber = nextVersion, FieldCount = schema.Fields.Count, WorkflowDefinitionId = Input.WorkflowDefinitionId });

        return RedirectToPage("./Index");
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
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();
    }
}

public class EditFormInput
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

    public List<EditFieldInput> Fields { get; set; } = [];
}

public class EditFieldInput
{
    public string? Label { get; set; }

    public string? Key { get; set; }

    public FieldType Type { get; set; } = FieldType.Text;

    public bool Required { get; set; }

    public string? Options { get; set; }
}
