using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Forms;

public class SubmitModel(
    MatterForgeDbContext db,
    WorkflowService workflowService,
    ProductPlanService productPlanService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    [Required]
    [Display(Name = "Submitted by")]
    public Guid? SubmitterUserId { get; set; }

    public FormDefinition? Form { get; private set; }

    public FormSchema? Schema { get; private set; }

    public List<SelectListItem> SubmitterOptions { get; private set; } = [];

    public List<ClientSuggestion> ClientSuggestions { get; private set; } = [];

    public int VersionNumber { get; private set; }

    public Dictionary<string, string> PostedValues { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadFormAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadFormAsync();
        if (Form is null || Schema is null)
        {
            return Page();
        }

        var submitter = SubmitterUserId.HasValue
            ? await db.Users.FirstOrDefaultAsync(x => x.Id == SubmitterUserId.Value && x.IsActive)
            : null;

        if (submitter is null)
        {
            ModelState.AddModelError("SubmitterUserId", "Choose an active CMIForge user.");
        }

        PostedValues = Request.Form
            .Where(x => x.Key.StartsWith("Fields[", StringComparison.Ordinal))
            .ToDictionary(
                x => x.Key["Fields[".Length..^1],
                x => x.Value.ToString());

        foreach (var required in Schema.Fields.Where(x => x.Required))
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
            field => field.Type == FieldType.Checkbox
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
            DataJson = JsonSerializer.Serialize(answers, FormJson.Options)
        };

        db.FormSubmissions.Add(submission);

        await db.SaveChangesAsync();
        if (productPlanService.AllowsFeature(ProductFeatureKeys.Workflow))
        {
            await workflowService.EnsureStartedAsync(submission);
        }

        return RedirectToPage("/Submissions/Index");
    }

    private async Task LoadFormAsync()
    {
        ClientSuggestions = await db.Clients
            .OrderBy(x => x.Name)
            .Select(x => new ClientSuggestion(
                x.Name,
                $"{x.ClientNumber:D8} - {x.Name}"))
            .ToListAsync();

        SubmitterOptions = await db.Users
            .Where(x => x.IsActive)
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
}

public record ClientSuggestion(string Name, string DisplayLabel);
