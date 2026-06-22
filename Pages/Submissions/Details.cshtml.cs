using Azure;
using System.Text.Json;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Submissions;

public class DetailsModel(
    CMIForgeDbContext db,
    WorkflowService workflowService,
    CurrentUserService currentUserService,
    PermissionService permissionService,
    SubmissionAttachmentService attachmentService,
    AuditLogService auditLogService) : PageModel
{
    public FormSubmission? Submission { get; private set; }

    public List<SubmissionAnswer> Answers { get; private set; } = [];

    public string SubmittedClientName { get; private set; } = string.Empty;

    public string SubmittedMatterName { get; private set; } = string.Empty;

    public FormSchema? Schema { get; private set; }

    public Dictionary<string, string> EditValues { get; private set; } = [];

    public List<SubmissionAttachment> Attachments { get; private set; } = [];

    public List<SubmissionWorkflowInstance> WorkflowInstances { get; private set; } = [];

    public List<SubmissionWorkflowTask> WorkflowTasks { get; private set; } = [];

    public List<SubmissionWorkflowEvent> WorkflowEvents { get; private set; } = [];

    public List<ConflictSearch> ConflictSearches { get; private set; } = [];

    public List<AuditLog> AuditHistory { get; private set; } = [];

    public InboundEmailMessage? InboundEmailSource { get; private set; }

    public HashSet<string> EditableStepNames { get; private set; } = [];

    public bool CanConvert { get; private set; }

    public bool CanRunConflicts { get; private set; }

    public bool CanManageAttachments { get; private set; }

    public bool CanEditReturned { get; private set; }

    public bool CanCancel { get; private set; }

    public bool CanStartWorkflow { get; private set; }

    [BindProperty]
    public Dictionary<string, string> Fields { get; set; } = [];

    [BindProperty]
    public string Notes { get; set; } = string.Empty;

    [BindProperty]
    public IFormFile? AttachmentFile { get; set; }

    [BindProperty]
    public string? AttachmentDisplayName { get; set; }

    [BindProperty]
    public string LinkUrl { get; set; } = string.Empty;

    [BindProperty]
    public string? LinkDisplayName { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        await LoadSubmissionAsync(id);
        if (Submission is null)
        {
            return Page();
        }

        if (!await CanAccessSubmissionAsync(Submission))
        {
            return Forbid();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(Guid id, string status)
    {
        if (!SubmissionStatuses.IsValid(status) || status == SubmissionStatuses.Converted)
        {
            return BadRequest();
        }

        var submission = await db.FormSubmissions.FirstOrDefaultAsync(x => x.Id == id);
        if (submission is null)
        {
            return NotFound();
        }

        if (!await CanAccessSubmissionAsync(submission) ||
            !await permissionService.HasAsync(PermissionKeys.SubmissionsApprove))
        {
            return Forbid();
        }

        var oldStatus = submission.Status;
        submission.Status = status;
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Submission.StatusChanged",
            "Submission",
            submission.Id,
            RecordNumbers.Submission(submission.SubmissionNumber),
            $"Changed submission status from {oldStatus} to {status}.",
            new { OldStatus = oldStatus, NewStatus = status });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostStartWorkflowAsync(Guid id)
    {
        var submission = await db.FormSubmissions.FirstOrDefaultAsync(x => x.Id == id);
        if (submission is null)
        {
            return NotFound();
        }

        if (!await CanAccessSubmissionAsync(submission))
        {
            return Forbid();
        }

        if (!await workflowService.CanStartAsync(submission))
        {
            ModelState.AddModelError(string.Empty, "No active workflow is available for this submission.");
            await LoadSubmissionAsync(id);
            return Page();
        }

        var instance = await workflowService.EnsureStartedAsync(id);
        if (instance is null)
        {
            ModelState.AddModelError(string.Empty, "The workflow could not be started. Check that the form has an active workflow with at least one matching step.");
            await LoadSubmissionAsync(id);
            return Page();
        }

        await auditLogService.LogAsync(
            "Workflow.Started",
            "Submission",
            id,
            RecordNumbers.Submission(submission.SubmissionNumber),
            "Started submission workflow.",
            new { WorkflowInstanceId = instance.Id });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id)
    {
        var submission = await db.FormSubmissions
            .Include(x => x.WorkflowInstances)
            .Include(x => x.WorkflowTasks)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (submission is null)
        {
            return NotFound();
        }

        if (!await CanCancelSubmissionAsync(submission))
        {
            return Forbid();
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        var oldStatus = submission.Status;
        submission.Status = SubmissionStatuses.Cancelled;

        var affectedInstances = submission.WorkflowInstances
            .Where(x => x.Status is WorkflowStatuses.Active or WorkflowStatuses.Returned)
            .ToList();
        foreach (var instance in affectedInstances)
        {
            instance.Status = WorkflowStatuses.Cancelled;
            instance.CompletedAt = DateTimeOffset.UtcNow;
            db.SubmissionWorkflowEvents.Add(new SubmissionWorkflowEvent
            {
                SubmissionWorkflowInstanceId = instance.Id,
                FormSubmissionId = submission.Id,
                EventType = WorkflowStatuses.EventCancelled,
                Message = "Submission cancelled.",
                ActorUserId = currentUser?.Id
            });
        }

        foreach (var task in submission.WorkflowTasks.Where(x => x.Status is WorkflowStatuses.TaskOpen or WorkflowStatuses.TaskReturned))
        {
            task.Status = WorkflowStatuses.TaskCancelled;
            task.Outcome = WorkflowStatuses.TaskCancelled;
            task.CompletedAt = DateTimeOffset.UtcNow;
            task.CompletedByUserId = currentUser?.Id;
        }

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Submission.Cancelled",
            "Submission",
            submission.Id,
            RecordNumbers.Submission(submission.SubmissionNumber),
            $"Cancelled submission that was {oldStatus}.",
            new { OldStatus = oldStatus, NewStatus = submission.Status });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEditReturnedAsync(Guid id)
    {
        var submission = await db.FormSubmissions
            .Include(x => x.FormVersion)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (submission?.FormVersion is null)
        {
            return NotFound();
        }

        if (!await CanAccessSubmissionAsync(submission) ||
            submission.Status != SubmissionStatuses.Returned ||
            submission.ClientId.HasValue ||
            submission.MatterId.HasValue)
        {
            return Forbid();
        }

        var schema = FormJson.DeserializeSchema(submission.FormVersion.SchemaJson);
        Fields = ReadPostedValues(schema, Request.Form);
        ModelState.Clear();

        var editableStepNames = await GetEditableStepNamesAsync(submission.Id);
        var existingValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(submission.DataJson, FormJson.Options)?
            .ToDictionary(x => x.Key, x => SubmissionAnswerReader.FormatValue(x.Value)) ?? [];

        foreach (var field in schema.Fields.Where(x => !FormFieldRules.IsEditableForWorkflowStep(x, editableStepNames)))
        {
            Fields[field.Key] = existingValues.GetValueOrDefault(field.Key, string.Empty);
        }

        foreach (var required in schema.Fields.Where(x => x.Required && FormFieldRules.IsVisible(x, Fields)))
        {
            if (!Fields.TryGetValue(required.Key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                ModelState.AddModelError(string.Empty, $"{required.Label} is required.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadSubmissionAsync(id);
            EditValues = Fields;
            return Page();
        }

        var answers = schema.Fields.ToDictionary<FormField, string, object?>(
            field => field.Key,
            field => !FormFieldRules.IsVisible(field, Fields)
                ? null
                : field.Type == FieldType.Checkbox
                ? Fields.TryGetValue(field.Key, out var checkboxValue) && checkboxValue.Equals("true", StringComparison.OrdinalIgnoreCase)
                : Fields.GetValueOrDefault(field.Key));

        submission.DataJson = JsonSerializer.Serialize(answers, FormJson.Options);
        var oldStatus = submission.Status;
        submission.Status = SubmissionStatuses.Submitted;

        var returnedInstances = await db.SubmissionWorkflowInstances
            .Where(x => x.FormSubmissionId == submission.Id && x.Status == WorkflowStatuses.Returned)
            .ToListAsync();
        foreach (var instance in returnedInstances)
        {
            instance.Status = WorkflowStatuses.Active;
            instance.CompletedAt = null;
        }

        var returnedTasks = await db.SubmissionWorkflowTasks
            .Where(x => x.FormSubmissionId == submission.Id && x.Status == WorkflowStatuses.TaskReturned)
            .ToListAsync();
        foreach (var task in returnedTasks)
        {
            task.Status = WorkflowStatuses.TaskOpen;
            task.Outcome = string.Empty;
            task.CompletedAt = null;
            task.CompletedByUserId = null;
        }

        foreach (var instance in returnedInstances)
        {
            db.SubmissionWorkflowEvents.Add(new SubmissionWorkflowEvent
            {
                SubmissionWorkflowInstanceId = instance.Id,
                FormSubmissionId = submission.Id,
                EventType = "Resubmitted",
                Message = "Returned submission edited and resubmitted."
            });
        }

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Submission.ReturnedEdited",
            "Submission",
            submission.Id,
            RecordNumbers.Submission(submission.SubmissionNumber),
            $"Edited returned submission and changed status from {oldStatus} to {submission.Status}.");

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostOutcomeTaskAsync(Guid id, Guid taskId, string outcomeKey)
    {
        var task = await db.SubmissionWorkflowTasks.FirstOrDefaultAsync(x => x.Id == taskId);
        if (task is null || !await permissionService.CanActOnTaskAsync(task))
        {
            return Forbid();
        }

        await workflowService.ApplyOutcomeAsync(taskId, outcomeKey, Notes, (await currentUserService.GetCurrentUserAsync())?.Id);
        await auditLogService.LogAsync(
            "WorkflowTask.Outcome",
            "Submission",
            id,
            null,
            $"Applied workflow outcome {outcomeKey}.",
            new { TaskId = taskId, Notes });
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUploadAttachmentAsync(Guid id)
    {
        var submissionId = ResolveSubmissionId(id);
        var submission = await db.FormSubmissions.FirstOrDefaultAsync(x => x.Id == submissionId);
        if (submission is null)
        {
            return NotFound();
        }

        if (!await CanAccessSubmissionAsync(submission))
        {
            return Forbid();
        }

        if (AttachmentFile is null)
        {
            ModelState.AddModelError(string.Empty, "Choose a PDF, Word, or Excel file before uploading.");
            await LoadSubmissionAsync(submissionId);
            return Page();
        }

        try
        {
            var currentUser = await currentUserService.GetCurrentUserAsync();
            var attachment = await attachmentService.UploadFileAsync(
                submission,
                AttachmentFile,
                AttachmentDisplayName ?? string.Empty,
                currentUser?.Id);

            db.SubmissionAttachments.Add(attachment);
            try
            {
                await db.SaveChangesAsync();
                await auditLogService.LogAsync(
                    "Attachment.Uploaded",
                    "Submission",
                    submission.Id,
                    RecordNumbers.Submission(submission.SubmissionNumber),
                    $"Uploaded attachment {attachment.DisplayName}.",
                    new { attachment.OriginalFileName, attachment.SizeBytes, attachment.ContentType });
            }
            catch
            {
                await attachmentService.DeleteFileIfExistsAsync(attachment);
                throw;
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadSubmissionAsync(submissionId);
            return Page();
        }

        return RedirectToPage(new { id = submissionId });
    }

    public async Task<IActionResult> OnPostAddAttachmentLinkAsync(Guid id)
    {
        var submissionId = ResolveSubmissionId(id);
        var submission = await db.FormSubmissions.FirstOrDefaultAsync(x => x.Id == submissionId);
        if (submission is null)
        {
            return NotFound();
        }

        if (!await CanAccessSubmissionAsync(submission))
        {
            return Forbid();
        }

        try
        {
            var currentUser = await currentUserService.GetCurrentUserAsync();
            var attachment = attachmentService.CreateLink(submission, LinkUrl, LinkDisplayName ?? string.Empty, currentUser?.Id);
            db.SubmissionAttachments.Add(attachment);
            await db.SaveChangesAsync();
            await auditLogService.LogAsync(
                "AttachmentLink.Added",
                "Submission",
                submission.Id,
                RecordNumbers.Submission(submission.SubmissionNumber),
                $"Added attachment link {attachment.DisplayName}.",
                new { attachment.Url });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadSubmissionAsync(submissionId);
            return Page();
        }

        return RedirectToPage(new { id = submissionId });
    }

    public async Task<IActionResult> OnGetDownloadAttachmentAsync(Guid id, Guid attachmentId)
    {
        var submissionId = ResolveSubmissionId(id);
        var submission = await db.FormSubmissions.FirstOrDefaultAsync(x => x.Id == submissionId);
        if (submission is null)
        {
            return NotFound();
        }

        if (!await CanAccessSubmissionAsync(submission))
        {
            return Forbid();
        }

        var attachment = await db.SubmissionAttachments.FirstOrDefaultAsync(x =>
            x.Id == attachmentId &&
            x.FormSubmissionId == submissionId);
        if (attachment is null)
        {
            return NotFound();
        }

        if (attachment.AttachmentType == SubmissionAttachmentTypes.Link)
        {
            await auditLogService.LogAsync(
                "AttachmentLink.Opened",
                "Submission",
                submission.Id,
                RecordNumbers.Submission(submission.SubmissionNumber),
                $"Opened attachment link {attachment.DisplayName}.");
            return Redirect(attachment.Url);
        }

        try
        {
            var stream = await attachmentService.OpenReadAsync(attachment);
            await auditLogService.LogAsync(
                "Attachment.Downloaded",
                "Submission",
                submission.Id,
                RecordNumbers.Submission(submission.SubmissionNumber),
                $"Downloaded attachment {attachment.DisplayName}.",
                new { attachment.OriginalFileName, attachment.SizeBytes, attachment.ContentType });
            return File(stream, attachment.ContentType, attachment.OriginalFileName);
        }
        catch (RequestFailedException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    public async Task<IActionResult> OnPostDeleteAttachmentAsync(Guid id, Guid attachmentId)
    {
        var submissionId = ResolveSubmissionId(id);
        var submission = await db.FormSubmissions.FirstOrDefaultAsync(x => x.Id == submissionId);
        if (submission is null)
        {
            return NotFound();
        }

        if (!await CanAccessSubmissionAsync(submission))
        {
            return Forbid();
        }

        var attachment = await db.SubmissionAttachments.FirstOrDefaultAsync(x =>
            x.Id == attachmentId &&
            x.FormSubmissionId == submissionId);
        if (attachment is null)
        {
            return NotFound();
        }

        await attachmentService.DeleteFileIfExistsAsync(attachment);
        db.SubmissionAttachments.Remove(attachment);
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Attachment.Deleted",
            "Submission",
            submission.Id,
            RecordNumbers.Submission(submission.SubmissionNumber),
            $"Deleted attachment {attachment.DisplayName}.",
            new { attachment.OriginalFileName, attachment.AttachmentType, attachment.SizeBytes });

        return RedirectToPage(new { id = submissionId });
    }

    private async Task LoadSubmissionAsync(Guid id)
    {
        Submission = await db.FormSubmissions
            .Include(x => x.FormDefinition)
            .Include(x => x.FormVersion)
            .Include(x => x.SubmitterUser)
            .Include(x => x.LeadPartner)
            .Include(x => x.Client)
            .Include(x => x.Matter)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Submission?.FormVersion is null)
        {
            return;
        }

        CanManageAttachments = await CanAccessSubmissionAsync(Submission);
        CanConvert =
            Submission.Status == SubmissionStatuses.Approved &&
            !Submission.ClientId.HasValue &&
            !Submission.MatterId.HasValue &&
            await permissionService.HasAsync(PermissionKeys.SubmissionsConvert);
        CanRunConflicts =
            Submission.Status != SubmissionStatuses.Cancelled &&
            await permissionService.HasAsync(PermissionKeys.ConflictsRun);

        WorkflowInstances = await db.SubmissionWorkflowInstances
            .Include(x => x.WorkflowDefinition)
            .Where(x => x.FormSubmissionId == id)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync();

        WorkflowTasks = await db.SubmissionWorkflowTasks
            .Include(x => x.WorkflowStep)
            .Include(x => x.AssignedUser)
            .Include(x => x.AssignedTeam)
            .Include(x => x.CompletedByUser)
            .Where(x => x.FormSubmissionId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        EditableStepNames = WorkflowTasks
            .Where(x => x.Status == WorkflowStatuses.TaskOpen && x.WorkflowStep is not null)
            .Select(x => x.WorkflowStep!.Name.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        WorkflowEvents = await db.SubmissionWorkflowEvents
            .Include(x => x.ActorUser)
            .Where(x => x.FormSubmissionId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        ConflictSearches = await db.ConflictSearches
            .Include(x => x.Results)
            .Where(x => x.FormSubmissionId == id)
            .OrderByDescending(x => x.SearchNumber)
            .ToListAsync();

        Attachments = await db.SubmissionAttachments
            .Include(x => x.UploadedByUser)
            .Where(x => x.FormSubmissionId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        AuditHistory = await auditLogService.ListForEntityAsync("Submission", id, 8);
        InboundEmailSource = await db.InboundEmailMessages
            .AsNoTracking()
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.FormSubmissionId == Submission.Id);

        Schema = FormJson.DeserializeSchema(Submission.FormVersion.SchemaJson);
        ApplyLegacyDisplaySections(Schema);
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Submission.DataJson, FormJson.Options) ?? [];
        EditValues = values.ToDictionary(x => x.Key, x => SubmissionAnswerReader.FormatValue(x.Value));
        var normalizedAnswers = SubmissionAnswerReader.Read(Submission.DataJson);
        SubmittedClientName = SubmissionAnswerReader.FirstValue(normalizedAnswers, "clientName", "client", "companyName");
        SubmittedMatterName = SubmissionAnswerReader.FirstValue(normalizedAnswers, "matterName", "matter");

        var sectionsByKey = Schema.ResolvedSections.ToDictionary(x => x.Key, x => x.Label, StringComparer.OrdinalIgnoreCase);
        Answers = Schema.Fields
            .Select(field => new SubmissionAnswer(
                field.Label,
                values.TryGetValue(field.Key, out var value) ? SubmissionAnswerReader.FormatValue(value) : string.Empty,
                string.IsNullOrWhiteSpace(field.SectionKey) ? FormSchema.DefaultSectionKey : field.SectionKey,
                sectionsByKey.GetValueOrDefault(field.SectionKey, "General")))
            .ToList();

        CanEditReturned =
            Submission.Status == SubmissionStatuses.Returned &&
            !Submission.ClientId.HasValue &&
            !Submission.MatterId.HasValue &&
            await CanAccessSubmissionAsync(Submission);

        CanCancel = await CanCancelSubmissionAsync(Submission);

        CanStartWorkflow =
            (WorkflowInstances.Count == 0 || WorkflowInstances.All(x => x.Status == WorkflowStatuses.Cancelled)) &&
            await workflowService.CanStartAsync(Submission);
    }

    private static void ApplyLegacyDisplaySections(FormSchema schema)
    {
        var hasExplicitSections = schema.ResolvedSections.Count > 1 ||
            schema.Fields.Any(field => !string.IsNullOrWhiteSpace(field.SectionKey) &&
                !field.SectionKey.Equals(FormSchema.DefaultSectionKey, StringComparison.OrdinalIgnoreCase));
        if (hasExplicitSections)
        {
            return;
        }

        var usedSections = new List<FormSection>();
        foreach (var field in schema.Fields)
        {
            var section = ResolveLegacyDisplaySection(field);
            field.SectionKey = section.Key;
            if (!usedSections.Any(x => x.Key.Equals(section.Key, StringComparison.OrdinalIgnoreCase)))
            {
                usedSections.Add(section);
            }
        }

        schema.Sections = usedSections.Count == 0
            ? [new FormSection { Key = FormSchema.DefaultSectionKey, Label = "General" }]
            : usedSections;
    }

    private static FormSection ResolveLegacyDisplaySection(FormField field)
    {
        var text = $"{field.Key} {field.Label}".ToLowerInvariant();
        if (ContainsAny(text, "adverse", "affiliate", "counsel", "opposing", "parent", "party", "parties", "related", "relationship", "subsidiary"))
        {
            return new FormSection { Key = "related-parties", Label = "Related Parties" };
        }

        if (ContainsAny(text, "conflict", "clearance", "search"))
        {
            return new FormSection { Key = "conflicts-search", Label = "Conflicts Search" };
        }

        if (ContainsAny(text, "approval", "approve", "compliance", "kyc", "aml", "review", "risk", "source of funds", "source-of-funds", "terms"))
        {
            return new FormSection { Key = "compliance-review", Label = "Compliance Review" };
        }

        if (ContainsAny(text, "client", "company", "corporate", "dob", "birth", "registration", "trading", "organisation", "organization"))
        {
            return new FormSection { Key = "client-details", Label = "Client Details" };
        }

        if (ContainsAny(text, "matter", "practice", "fee", "summary", "scope", "instructions", "engagement"))
        {
            return new FormSection { Key = "matter-details", Label = "Matter Details" };
        }

        return new FormSection { Key = "review", Label = "Review" };
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(value.Contains);
    }

    private async Task<bool> CanCancelSubmissionAsync(FormSubmission submission)
    {
        if (submission.Status is SubmissionStatuses.Converted or SubmissionStatuses.Cancelled)
        {
            return false;
        }

        if (!await CanAccessSubmissionAsync(submission))
        {
            return false;
        }

        if (await permissionService.HasAsync(PermissionKeys.SubmissionsApprove))
        {
            return true;
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        return currentUser is not null && submission.SubmitterUserId == currentUser.Id;
    }

    private static Dictionary<string, string> ReadPostedValues(FormSchema schema, IFormCollection form)
    {
        return schema.Fields.ToDictionary(
            field => field.Key,
            field => field.Type == FieldType.Address
                ? FormAddressValue.Compose(FormAddressValue.FromForm(form, field.Key))
                : form[$"Fields[{field.Key}]"].LastOrDefault() ?? string.Empty);
    }

    private async Task<HashSet<string>> GetEditableStepNamesAsync(Guid submissionId)
    {
        return (await db.SubmissionWorkflowTasks
                .AsNoTracking()
                .Include(x => x.WorkflowStep)
                .Where(x => x.FormSubmissionId == submissionId && x.WorkflowStep != null && x.Status == WorkflowStatuses.TaskOpen)
                .Select(x => x.WorkflowStep!.Name)
                .ToListAsync())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<bool> CanAccessSubmissionAsync(FormSubmission submission)
    {
        if (await permissionService.HasAsync(PermissionKeys.SubmissionsViewAll))
        {
            return true;
        }

        if (!await permissionService.HasAsync(PermissionKeys.SubmissionsViewOwn))
        {
            return false;
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        return currentUser is not null && submission.SubmitterUserId == currentUser.Id;
    }

    private Guid ResolveSubmissionId(Guid id)
    {
        if (id != Guid.Empty)
        {
            return id;
        }

        if (RouteData.Values.TryGetValue("id", out var routeId) &&
            Guid.TryParse(Convert.ToString(routeId), out var parsedRouteId))
        {
            return parsedRouteId;
        }

        if (Guid.TryParse(Request.Query["id"].FirstOrDefault(), out var parsedQueryId))
        {
            return parsedQueryId;
        }

        if (Request.HasFormContentType &&
            Guid.TryParse(Request.Form["id"].FirstOrDefault(), out var parsedFormId))
        {
            return parsedFormId;
        }

        return Guid.Empty;
    }

    public static string FormatAttachmentSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var kb = bytes / 1024d;
        if (kb < 1024)
        {
            return $"{kb:0.#} KB";
        }

        return $"{kb / 1024d:0.##} MB";
    }
}

public record SubmissionAnswer(
    string Label,
    string Value,
    string SectionKey = FormSchema.DefaultSectionKey,
    string SectionLabel = "General");
