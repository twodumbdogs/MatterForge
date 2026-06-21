using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Forms;

public class SubmitModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    ConflictSearchService conflictSearchService,
    WorkflowService workflowService,
    ProductPlanService productPlanService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    [Required]
    [Display(Name = "Submitted by")]
    public Guid? SubmitterUserId { get; set; }

    [BindProperty]
    [Display(Name = "Lead partner")]
    public Guid? LeadPartnerId { get; set; }

    public FormDefinition? Form { get; private set; }

    public FormSchema? Schema { get; private set; }

    public List<SelectListItem> SubmitterOptions { get; private set; } = [];

    public List<SelectListItem> PartnerOptions { get; private set; } = [];

    public List<ClientSuggestion> ClientSuggestions { get; private set; } = [];

    public int VersionNumber { get; private set; }

    public Dictionary<string, string> PostedValues { get; private set; } = [];

    public bool IsConflictPreviewEnabled { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.FormsSubmit))
        {
            return Forbid();
        }

        await LoadFormAsync();
        return Page();
    }

    public async Task<IActionResult> OnGetConflictPreviewAsync(Guid id, string? terms)
    {
        if (!await permissionService.HasAsync(PermissionKeys.FormsSubmit) &&
            !await permissionService.HasAsync(PermissionKeys.ConflictsView))
        {
            return Forbid();
        }

        if (!await IsConflictPreviewEnabledAsync())
        {
            return NotFound();
        }

        var formId = id == Guid.Empty ? Id : id;
        var formExists = await db.FormDefinitions.AnyAsync(x =>
            x.Id == formId &&
            x.IsActive &&
            x.Versions.Any(v => v.IsPublished));

        if (!formExists)
        {
            return NotFound();
        }

        var preview = await conflictSearchService.PreviewAsync(terms ?? string.Empty);
        return new JsonResult(preview, FormJson.Options);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.FormsSubmit))
        {
            return Forbid();
        }

        await LoadFormAsync();
        if (Form is null || Schema is null)
        {
            return Page();
        }

        var submitter = SubmitterUserId.HasValue
            ? await db.Users.FirstOrDefaultAsync(x => x.Id == SubmitterUserId.Value && x.IsActive && !x.IsArchived)
            : null;

        if (submitter is null)
        {
            ModelState.AddModelError("SubmitterUserId", "Choose an active CMIForge user.");
        }

        if (LeadPartnerId.HasValue && !await IsPartnerAsync(LeadPartnerId.Value))
        {
            ModelState.AddModelError("LeadPartnerId", "Choose a user with the Partner role.");
        }

        PostedValues = ReadPostedValues(Schema, Request.Form);

        foreach (var required in Schema.Fields.Where(x => x.Required && FormFieldRules.IsVisible(x, PostedValues)))
        {
            if (!PostedValues.TryGetValue(required.Key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                ModelState.AddModelError(string.Empty, $"{required.Label} is required.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var answers = Schema.Fields.ToDictionary<FormField, string, object?>(
            field => field.Key,
            field => !FormFieldRules.IsVisible(field, PostedValues)
                ? null
                : field.Type == FieldType.Checkbox
                ? PostedValues.ContainsKey(field.Key)
                : PostedValues.GetValueOrDefault(field.Key));

        var latestVersion = Form.Versions
            .Where(x => x.IsPublished)
            .OrderByDescending(x => x.VersionNumber)
            .First();

        var nextSubmissionNumber = (await db.FormSubmissions.MaxAsync(x => (int?)x.SubmissionNumber) ?? 0) + 1;

        var submission = new FormSubmission
        {
            SubmissionNumber = nextSubmissionNumber,
            FormDefinitionId = Form.Id,
            FormVersionId = latestVersion.Id,
            SubmitterUserId = submitter!.Id,
            SubmitterName = submitter.DisplayName,
            LeadPartnerId = LeadPartnerId,
            DataJson = JsonSerializer.Serialize(answers, FormJson.Options)
        };

        db.FormSubmissions.Add(submission);

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Submission.Created",
            "Submission",
            submission.Id,
            RecordNumbers.Submission(submission.SubmissionNumber),
            $"Created submission {RecordNumbers.Submission(submission.SubmissionNumber)}.",
            new { submission.FormDefinitionId, submission.SubmitterUserId, submission.LeadPartnerId });

        if (productPlanService.AllowsFeature(ProductFeatureKeys.Workflow))
        {
            await workflowService.EnsureStartedAsync(submission);
        }

        if (!await permissionService.HasAsync(PermissionKeys.SubmissionsViewOwn) &&
            !await permissionService.HasAsync(PermissionKeys.SubmissionsViewAll))
        {
            return RedirectToPage("./Submitted", new { submissionNumber = submission.SubmissionNumber });
        }

        return RedirectToPage("/Submissions/Index");
    }

    private async Task LoadFormAsync()
    {
        ClientSuggestions = await db.Clients
            .Where(x => !x.IsArchived)
            .OrderBy(x => x.Name)
            .Select(x => new ClientSuggestion(
                x.Name,
                $"{x.ClientNumber:D8} - {x.Name}"))
            .ToListAsync();

        IsConflictPreviewEnabled = await IsConflictPreviewEnabledAsync();

        SubmitterOptions = await db.Users
            .Where(x => x.IsActive && !x.IsArchived)
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem($"{x.DisplayName} ({x.SystemId:D8})", x.Id.ToString()))
            .ToListAsync();

        PartnerOptions = await db.Users
            .Where(x => x.IsActive && !x.IsArchived)
            .Where(x => x.Roles.Any(role => role.SecurityRole != null && role.SecurityRole.Key == SecurityRoleKeys.Partner && role.SecurityRole.IsActive))
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem($"{x.DisplayName} ({x.SystemId:D8})", x.Id.ToString()))
            .ToListAsync();

        Form = await db.FormDefinitions
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == Id && x.IsActive);

        var latestVersion = Form?.Versions
            .Where(x => x.IsPublished)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefault();

        if (latestVersion is null)
        {
            return;
        }

        VersionNumber = latestVersion.VersionNumber;
        Schema = FormJson.DeserializeSchema(latestVersion.SchemaJson);
    }

    private Task<bool> IsPartnerAsync(Guid userId)
    {
        return db.Users.AnyAsync(x =>
            x.Id == userId &&
            x.IsActive &&
            !x.IsArchived &&
            x.Roles.Any(role => role.SecurityRole != null && role.SecurityRole.Key == SecurityRoleKeys.Partner && role.SecurityRole.IsActive));
    }

    private async Task<bool> IsConflictPreviewEnabledAsync()
    {
        var value = await db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key == "Conflicts.LivePreviewEnabled")
            .Select(x => x.Value)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(value) || bool.TryParse(value, out var enabled) && enabled;
    }

    private static Dictionary<string, string> ReadPostedValues(FormSchema schema, IFormCollection form)
    {
        return schema.Fields.ToDictionary(
            field => field.Key,
            field => field.Type == FieldType.Address
                ? FormAddressValue.Compose(FormAddressValue.FromForm(form, field.Key))
                : form[$"Fields[{field.Key}]"].ToString());
    }
}

public record ClientSuggestion(string Name, string DisplayLabel);
