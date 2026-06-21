using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.External.Forms;

public class SubmitModel(
    CMIForgeDbContext db,
    WorkflowService workflowService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = string.Empty;

    [BindProperty]
    public Dictionary<string, string> Fields { get; set; } = [];

    public ExternalFormInvite? Invite { get; private set; }

    public FormDefinition? Form { get; private set; }

    public FormSchema? Schema { get; private set; }

    public Dictionary<string, string> PostedValues { get; private set; } = [];

    public string? UnavailableMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadInviteAsync(markOpened: true);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadInviteAsync(markOpened: false);
        if (Invite is null || Form is null || Schema is null)
        {
            return Page();
        }

        PostedValues = ReadPostedValues(Schema, Request.Form);
        Fields = PostedValues;

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
                ? PostedValues.TryGetValue(field.Key, out var checkboxValue) && checkboxValue.Equals("true", StringComparison.OrdinalIgnoreCase)
                : PostedValues.GetValueOrDefault(field.Key));

        var nextSubmissionNumber = (await db.FormSubmissions.MaxAsync(x => (int?)x.SubmissionNumber) ?? 0) + 1;
        var submitterName = string.IsNullOrWhiteSpace(Invite.RecipientName)
            ? Invite.RecipientEmail
            : $"{Invite.RecipientName} (client)";

        var submission = new FormSubmission
        {
            SubmissionNumber = nextSubmissionNumber,
            FormDefinitionId = Invite.FormDefinitionId,
            FormVersionId = Invite.FormVersionId,
            SubmitterName = submitterName,
            LeadPartnerId = Invite.LeadPartnerId,
            DataJson = JsonSerializer.Serialize(answers, FormJson.Options)
        };

        db.FormSubmissions.Add(submission);
        await db.SaveChangesAsync();

        Invite.Status = ExternalFormInviteStatuses.Completed;
        Invite.CompletedAt = DateTimeOffset.UtcNow;
        Invite.FormSubmissionId = submission.Id;
        Invite.UpdatedAt = Invite.CompletedAt.Value;
        await db.SaveChangesAsync();

        await auditLogService.LogExternalAsync(
            Invite.RecipientName,
            Invite.RecipientEmail,
            "ExternalFormInvite.Completed",
            "Submission",
            submission.Id,
            RecordNumbers.Submission(submission.SubmissionNumber),
            $"External client completed form {Form.Name}.",
            new
            {
                InviteId = Invite.Id,
                Invite.FormDefinitionId,
                Invite.FormVersionId,
                Invite.RecipientContactId
            });

        await workflowService.EnsureStartedAsync(submission);

        return RedirectToPage("/External/Forms/Submitted", new { submissionNumber = submission.SubmissionNumber });
    }

    private async Task LoadInviteAsync(bool markOpened)
    {
        Invite = null;
        Form = null;
        Schema = null;
        UnavailableMessage = null;

        if (string.IsNullOrWhiteSpace(Token))
        {
            UnavailableMessage = "This secure form link is missing its access token.";
            return;
        }

        var tokenHash = ExternalFormInviteTokenService.HashToken(Token);
        Invite = await db.ExternalFormInvites
            .Include(x => x.FormDefinition)
            .Include(x => x.FormVersion)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

        if (Invite is null)
        {
            UnavailableMessage = "This secure form link could not be found.";
            return;
        }

        if (Invite.Status == ExternalFormInviteStatuses.Completed)
        {
            UnavailableMessage = "This secure form has already been submitted.";
            return;
        }

        if (Invite.Status == ExternalFormInviteStatuses.Revoked)
        {
            UnavailableMessage = "This secure form link has been revoked.";
            return;
        }

        if (Invite.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            Invite.Status = ExternalFormInviteStatuses.Expired;
            Invite.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            UnavailableMessage = "This secure form link has expired.";
            return;
        }

        if (Invite.FormDefinition?.IsActive != true || Invite.FormVersion is null)
        {
            UnavailableMessage = "This form is no longer available.";
            return;
        }

        if (markOpened && Invite.OpenedAt is null)
        {
            Invite.OpenedAt = DateTimeOffset.UtcNow;
            Invite.UpdatedAt = Invite.OpenedAt.Value;
            await db.SaveChangesAsync();
        }

        Form = Invite.FormDefinition;
        Schema = FormJson.DeserializeSchema(Invite.FormVersion.SchemaJson);
    }

    private static Dictionary<string, string> ReadPostedValues(FormSchema schema, IFormCollection form)
    {
        return schema.Fields.ToDictionary(
            field => field.Key,
            field => field.Type == FieldType.Address
                ? FormAddressValue.Compose(FormAddressValue.FromForm(form, field.Key))
                : field.Type == FieldType.Checkbox
                    ? form[$"Fields[{field.Key}]"].Any(value => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)).ToString().ToLowerInvariant()
                : form[$"Fields[{field.Key}]"].ToString());
    }
}
