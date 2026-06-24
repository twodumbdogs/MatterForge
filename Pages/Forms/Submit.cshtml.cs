using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Forms;

public class SubmitModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    CurrentUserService currentUserService,
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
    public string SubmitterUserLookupText { get; set; } = string.Empty;

    [BindProperty]
    [Display(Name = "Lead partner")]
    public Guid? LeadPartnerId { get; set; }

    [BindProperty]
    public string LeadPartnerLookupText { get; set; } = string.Empty;

    public FormDefinition? Form { get; private set; }

    public FormSchema? Schema { get; private set; }

    public IReadOnlyList<FormSection> VisibleSections { get; private set; } = [];

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

    public async Task<IActionResult> OnGetClientSuggestionsAsync(Guid id, string? term)
    {
        if (!await permissionService.HasAsync(PermissionKeys.FormsSubmit))
        {
            return Forbid();
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

        var trimmedTerm = term?.Trim() ?? string.Empty;
        if (trimmedTerm.Length < 2)
        {
            return new JsonResult(Array.Empty<ClientSuggestion>(), FormJson.Options);
        }

        var escapedTerm = EscapeLikeValue(trimmedTerm);
        var clientNumberMatches = int.TryParse(trimmedTerm, out var parsedClientNumber)
            ? parsedClientNumber
            : (int?)null;

        var suggestions = await db.Clients
            .AsNoTracking()
            .Where(x => !x.IsArchived)
            .Where(x =>
                EF.Functions.Like(x.Name, $"%{escapedTerm}%", "\\") ||
                clientNumberMatches.HasValue && x.ClientNumber == clientNumberMatches.Value)
            .OrderBy(x => x.Name.StartsWith(trimmedTerm) ? 0 : 1)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.ClientNumber)
            .Take(8)
            .Select(x => new ClientSuggestion(
                x.Name,
                $"{x.ClientNumber:D8} - {x.Name}"))
            .ToListAsync();

        return new JsonResult(suggestions, FormJson.Options);
    }

    public async Task<IActionResult> OnGetUserSuggestionsAsync(Guid id, string? term, bool partnersOnly = false)
    {
        if (!await permissionService.HasAsync(PermissionKeys.FormsSubmit))
        {
            return Forbid();
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

        var trimmedTerm = term?.Trim() ?? string.Empty;
        if (trimmedTerm.Length < 2)
        {
            return new JsonResult(Array.Empty<UserSuggestion>(), FormJson.Options);
        }

        var escapedTerm = EscapeLikeValue(trimmedTerm);
        var systemIdMatch = int.TryParse(trimmedTerm, out var parsedSystemId)
            ? parsedSystemId
            : (int?)null;

        var query = db.Users
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsArchived);

        if (partnersOnly)
        {
            query = query.Where(x => x.Roles.Any(role =>
                role.SecurityRole != null &&
                role.SecurityRole.Key == SecurityRoleKeys.Partner &&
                role.SecurityRole.IsActive));
        }

        var suggestions = await query
            .Where(x =>
                EF.Functions.Like(x.DisplayName, $"%{escapedTerm}%", "\\") ||
                EF.Functions.Like(x.Email, $"%{escapedTerm}%", "\\") ||
                systemIdMatch.HasValue && x.SystemId == systemIdMatch.Value)
            .OrderBy(x => x.DisplayName.StartsWith(trimmedTerm) ? 0 : 1)
            .ThenBy(x => x.DisplayName)
            .ThenBy(x => x.SystemId)
            .Take(10)
            .Select(x => new UserSuggestion(
                x.Id,
                FormatUserLookupLabel(x.DisplayName, x.SystemId),
                string.IsNullOrWhiteSpace(x.Title)
                    ? $"{x.Email} · {x.SystemId:D8}"
                    : $"{x.Title} · {x.Email} · {x.SystemId:D8}"))
            .ToListAsync();

        return new JsonResult(suggestions, FormJson.Options);
    }

    private static string EscapeLikeValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);
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

        var visibleFields = VisibleFields();
        PostedValues = ReadPostedValues(visibleFields, Request.Form);

        foreach (var required in visibleFields.Where(x => x.Required && FormFieldRules.IsVisible(x, PostedValues)))
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

        var visibleFieldKeys = visibleFields.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var answers = Schema.Fields.ToDictionary<FormField, string, object?>(
            field => field.Key,
            field => !visibleFieldKeys.Contains(field.Key) || !FormFieldRules.IsVisible(field, PostedValues)
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
        IsConflictPreviewEnabled = await IsConflictPreviewEnabledAsync();

        var submitterDisplayName = await ResolveUserDisplayNameAsync(SubmitterUserId);
        if (!string.IsNullOrWhiteSpace(submitterDisplayName))
        {
            SubmitterUserLookupText = submitterDisplayName;
        }

        var leadPartnerDisplayName = await ResolveUserDisplayNameAsync(LeadPartnerId);
        if (!string.IsNullOrWhiteSpace(leadPartnerDisplayName))
        {
            LeadPartnerLookupText = leadPartnerDisplayName;
        }

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
        VisibleSections = FormSectionSecurity.VisibleSections(
            Schema,
            await currentUserService.GetCurrentUserTeamKeysAsync(),
            await permissionService.HasAsync(PermissionKeys.SecurityManage));
    }

    private Task<bool> IsPartnerAsync(Guid userId)
    {
        return db.Users.AnyAsync(x =>
            x.Id == userId &&
            x.IsActive &&
            !x.IsArchived &&
            x.Roles.Any(role => role.SecurityRole != null && role.SecurityRole.Key == SecurityRoleKeys.Partner && role.SecurityRole.IsActive));
    }

    private async Task<string> ResolveUserDisplayNameAsync(Guid? userId)
    {
        if (!userId.HasValue)
        {
            return string.Empty;
        }

        var user = await db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId.Value && x.IsActive && !x.IsArchived)
            .Select(x => new { x.DisplayName, x.SystemId })
            .FirstOrDefaultAsync();

        return user is null
            ? string.Empty
            : FormatUserLookupLabel(user.DisplayName, user.SystemId);
    }

    private static string FormatUserLookupLabel(string displayName, int systemId)
    {
        return $"{displayName} ({systemId:D8})";
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

    private IReadOnlyList<FormField> VisibleFields()
    {
        return Schema is null
            ? []
            : FormSectionSecurity.VisibleFields(Schema, VisibleSections);
    }

    private static Dictionary<string, string> ReadPostedValues(IEnumerable<FormField> fields, IFormCollection form)
    {
        return fields.ToDictionary(
            field => field.Key,
            field => field.Type == FieldType.Address
                ? FormAddressValue.Compose(FormAddressValue.FromForm(form, field.Key))
                : form[$"Fields[{field.Key}]"].ToString());
    }
}

public record ClientSuggestion(string Name, string DisplayLabel);

public record UserSuggestion(Guid Id, string Name, string DisplayLabel);
