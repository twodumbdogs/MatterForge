using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Text.Json;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Submissions;

public class ConvertModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    ConflictSearchService conflictSearchService,
    ProductPlanService productPlanService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public ConvertSubmissionInput Input { get; set; } = new();

    public FormSubmission? Submission { get; private set; }

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public List<SelectListItem> PartnerOptions { get; private set; } = [];

    public List<SelectListItem> ExistingClientOptions { get; private set; } = [];

    public string ExistingClientHint { get; private set; } = string.Empty;

    public List<SubmissionAnswer> SourceAnswers { get; private set; } = [];

    public bool CanConvert { get; private set; }

    public ProductLimitStatus MatterLimit { get; private set; } = new("matters", 0, null, true, string.Empty);

    public ProductLimitStatus ClientLimit { get; private set; } = new("clients", 0, null, true, string.Empty);

    public bool HasExistingClientMatches => ExistingClientOptions.Count > 0;

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadPageAsync();
        if (Submission is null)
        {
            return Page();
        }

        if (!CanConvert)
        {
            return Page();
        }

        Input = await BuildPrefillAsync(Submission);
        await LoadExistingClientReuseOptionsAsync(Input.ClientName, autoSelectSingleMatch: true);
        return Page();
    }

    public async Task<IActionResult> OnGetConflictPreviewAsync(Guid id, string? terms)
    {
        if (!await permissionService.HasAsync(PermissionKeys.SubmissionsConvert) &&
            !await permissionService.HasAsync(PermissionKeys.ConflictsView))
        {
            return Forbid();
        }

        var submissionId = id == Guid.Empty ? Id : id;
        var submissionExists = await db.FormSubmissions.AnyAsync(x => x.Id == submissionId);
        if (!submissionExists)
        {
            return NotFound();
        }

        var preview = await conflictSearchService.PreviewAsync(terms ?? string.Empty);
        return new JsonResult(preview, FormJson.Options);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadPageAsync();
        if (Submission is null)
        {
            return Page();
        }

        var existingClientMatches = await LoadExistingClientReuseOptionsAsync(Input.ClientName, autoSelectSingleMatch: false);

        var createMatter = !string.IsNullOrWhiteSpace(Input.MatterName);

        if (!CanConvert)
        {
            ModelState.AddModelError(string.Empty, "This submission must be approved before it can be converted.");
        }

        if (createMatter && !MatterLimit.CanCreate)
        {
            ModelState.AddModelError(string.Empty, MatterLimit.Message);
        }

        if (!Input.UseExistingClient && !ClientLimit.CanCreate)
        {
            ModelState.AddModelError(string.Empty, ClientLimit.Message);
        }

        if (Submission.ClientId.HasValue || Submission.MatterId.HasValue)
        {
            ModelState.AddModelError(string.Empty, "This submission is already linked to client or matter records.");
        }

        if (Input.ResponsibleUserId.HasValue && !await db.Users.AnyAsync(x => x.Id == Input.ResponsibleUserId.Value))
        {
            ModelState.AddModelError("Input.ResponsibleUserId", "Choose an existing user.");
        }

        if (Input.LeadPartnerId.HasValue && !await IsPartnerAsync(Input.LeadPartnerId.Value))
        {
            ModelState.AddModelError("Input.LeadPartnerId", "Choose a user with the Partner role.");
        }

        if (Input.UseExistingClient)
        {
            if (existingClientMatches.Count == 0)
            {
                ModelState.AddModelError("Input.UseExistingClient", "No existing client matches are available to reuse for that name.");
            }
            else if (!Input.ExistingClientId.HasValue)
            {
                ModelState.AddModelError("Input.ExistingClientId", "Choose which existing client record to reuse.");
            }
            else if (!existingClientMatches.Any(x => x.Id == Input.ExistingClientId.Value))
            {
                ModelState.AddModelError("Input.ExistingClientId", "Choose one of the matching existing client records.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Client client;
        if (Input.UseExistingClient && Input.ExistingClientId.HasValue)
        {
            client = await db.Clients.FirstAsync(x => x.Id == Input.ExistingClientId.Value);
        }
        else
        {
            var nextClientNumber = (await db.Clients.MaxAsync(x => (int?)x.ClientNumber) ?? 0) + 1;
            client = new Client
            {
                ClientNumber = nextClientNumber,
                Name = NormalizeClientLookupName(Input.ClientName),
                Status = Input.ClientStatus,
                PrimaryContact = Input.PrimaryContact?.Trim() ?? string.Empty,
                Email = Input.Email?.Trim() ?? string.Empty,
                Phone = Input.Phone?.Trim() ?? string.Empty,
                Notes = Input.ClientNotes?.Trim() ?? string.Empty
            };

            db.Clients.Add(client);
        }

        Matter? matter = null;
        if (createMatter)
        {
            var nextMatterNumber = (await db.Matters.MaxAsync(x => (int?)x.MatterNumber) ?? 0) + 1;
            matter = new Matter
            {
                MatterNumber = nextMatterNumber,
                Name = Input.MatterName.Trim(),
                Client = client,
                PracticeArea = Input.PracticeArea?.Trim() ?? string.Empty,
                Status = Input.MatterStatus,
                OpenedDate = Input.OpenedDate,
                ResponsibleUserId = Input.ResponsibleUserId,
                LeadPartnerId = Input.LeadPartnerId,
                Notes = Input.MatterNotes?.Trim() ?? string.Empty
            };

            db.Matters.Add(matter);
            await conflictSearchService.EnsureMatterClientPartyAsync(client, matter);
        }

        Submission.Client = client;
        Submission.Matter = matter;
        Submission.Status = SubmissionStatuses.Converted;

        await db.SaveChangesAsync();
        var convertedSummary = matter is null
            ? $"Converted submission to client {client.ClientNumber:D8}."
            : $"Converted submission to client {client.ClientNumber:D8} and matter {matter.MatterNumber:D8}.";
        await auditLogService.LogAsync(
            "Submission.Converted",
            "Submission",
            Submission.Id,
            RecordNumbers.Submission(Submission.SubmissionNumber),
            convertedSummary,
            new
            {
                ClientId = client.Id,
                ClientNumber = client.ClientNumber,
                MatterId = matter?.Id,
                MatterNumber = matter?.MatterNumber,
                ReusedClient = Input.UseExistingClient
            });

        return RedirectToPage("./Details", new { id = Submission.Id });
    }

    private async Task LoadPageAsync()
    {
        Submission = await db.FormSubmissions
            .Include(x => x.FormDefinition)
            .Include(x => x.FormVersion)
            .Include(x => x.Client)
            .Include(x => x.Matter)
            .FirstOrDefaultAsync(x => x.Id == Id);

        UserOptions = await db.Users
            .Where(x => x.IsActive && !x.IsArchived)
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem(x.DisplayName, x.Id.ToString()))
            .ToListAsync();

        PartnerOptions = await db.Users
            .Where(x => x.IsActive && !x.IsArchived)
            .Where(x => x.Roles.Any(role => role.SecurityRole != null && role.SecurityRole.Key == SecurityRoleKeys.Partner && role.SecurityRole.IsActive))
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem(x.DisplayName, x.Id.ToString()))
            .ToListAsync();
        MatterLimit = await productPlanService.GetMatterLimitAsync();
        ClientLimit = await productPlanService.GetClientLimitAsync();

        if (Submission?.FormVersion is null)
        {
            return;
        }

        CanConvert =
            Submission.Status == SubmissionStatuses.Approved &&
            !Submission.ClientId.HasValue &&
            !Submission.MatterId.HasValue &&
            await permissionService.HasAsync(PermissionKeys.SubmissionsConvert);

        var schema = FormJson.DeserializeSchema(Submission.FormVersion.SchemaJson);
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Submission.DataJson, FormJson.Options) ?? [];

        SourceAnswers = schema.Fields
            .Select(field => new SubmissionAnswer(
                field.Label,
                values.TryGetValue(field.Key, out var value) ? SubmissionAnswerReader.FormatValue(value) : string.Empty))
            .ToList();
    }

    private async Task<ConvertSubmissionInput> BuildPrefillAsync(FormSubmission submission)
    {
        var answers = SubmissionAnswerReader.Read(submission.DataJson);
        var assignedUser = SubmissionAnswerReader.FirstValue(answers, "assignedUser", "responsibleUser", "user", "attorney");
        var responsibleUserId = string.IsNullOrWhiteSpace(assignedUser)
            ? null
            : await db.Users
                .Where(x => x.IsActive && !x.IsArchived && x.DisplayName == assignedUser)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync();

        var summary = SubmissionAnswerReader.FirstValue(answers, "summary", "matterSummary", "description", "notes");

        return new ConvertSubmissionInput
        {
            ClientName = SubmissionAnswerReader.FirstValue(answers, "clientName", "client", "companyName", "name"),
            ClientStatus = "Active",
            PrimaryContact = SubmissionAnswerReader.FirstValue(answers, "primaryContact", "contactName", "contact"),
            Email = SubmissionAnswerReader.FirstValue(answers, "email", "clientEmail", "contactEmail"),
            Phone = SubmissionAnswerReader.FirstValue(answers, "phone", "clientPhone", "contactPhone"),
            ClientNotes = $"Created from submission {submission.Id}",
            MatterName = SubmissionAnswerReader.FirstValue(answers, "matterName", "matter", "caseName"),
            MatterStatus = "Open",
            PracticeArea = SubmissionAnswerReader.FirstValue(answers, "practiceArea", "practice", "area"),
            OpenedDate = DateOnly.FromDateTime(DateTime.Today),
            ResponsibleUserId = responsibleUserId,
            LeadPartnerId = submission.LeadPartnerId,
            MatterNotes = summary
        };
    }

    private Task<bool> IsPartnerAsync(Guid userId)
    {
        return db.Users.AnyAsync(x =>
            x.Id == userId &&
            x.IsActive &&
            !x.IsArchived &&
            x.Roles.Any(role => role.SecurityRole != null && role.SecurityRole.Key == SecurityRoleKeys.Partner && role.SecurityRole.IsActive));
    }

    private async Task<List<ExistingClientMatch>> LoadExistingClientReuseOptionsAsync(string? clientName, bool autoSelectSingleMatch)
    {
        ExistingClientOptions = [];
        ExistingClientHint = string.Empty;

        var lookup = ParseClientLookup(clientName);
        if (!lookup.ClientNumber.HasValue && string.IsNullOrWhiteSpace(lookup.Name))
        {
            return [];
        }

        var clients = await db.Clients
            .AsNoTracking()
            .Where(x => !x.IsArchived)
            .OrderBy(x => x.ClientNumber)
            .Select(x => new ExistingClientMatch(x.Id, x.ClientNumber, x.Name, x.Status))
            .ToListAsync();

        List<ExistingClientMatch> matches = [];
        if (lookup.ClientNumber.HasValue)
        {
            matches = clients
                .Where(x => x.ClientNumber == lookup.ClientNumber.Value)
                .ToList();
        }

        if (matches.Count == 0 && !string.IsNullOrWhiteSpace(lookup.Name))
        {
            var normalizedName = ConflictSearchService.NormalizeName(lookup.Name);
            matches = clients
                .Where(x => ConflictSearchService.NormalizeName(x.Name) == normalizedName)
                .ToList();
        }

        ExistingClientOptions = matches
            .Select(x => new SelectListItem($"{x.ClientNumber:D8} - {x.Name} ({x.Status})", x.Id.ToString()))
            .ToList();

        ExistingClientHint = matches.Count switch
        {
            0 => string.Empty,
            1 => "We found one exact client match and preselected it for reuse.",
            _ => $"We found {matches.Count} exact client matches. Pick one to reuse or leave reuse off to create a new client."
        };

        if (autoSelectSingleMatch && matches.Count == 1 && !Input.ExistingClientId.HasValue)
        {
            Input.UseExistingClient = true;
            Input.ExistingClientId = matches[0].Id;
        }

        return matches;
    }

    private static string NormalizeClientLookupName(string? clientName)
    {
        return ParseClientLookup(clientName).Name;
    }

    private static ClientLookup ParseClientLookup(string? clientName)
    {
        var trimmed = clientName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return new ClientLookup(string.Empty, null);
        }

        if (int.TryParse(trimmed, out var numericClientNumber))
        {
            return new ClientLookup(trimmed, numericClientNumber);
        }

        var numberPrefixedMatch = Regex.Match(trimmed, @"^(?<number>\d{8})\s*-\s*(?<name>.+)$");
        if (numberPrefixedMatch.Success &&
            int.TryParse(numberPrefixedMatch.Groups["number"].Value, out var prefixedClientNumber))
        {
            return new ClientLookup(numberPrefixedMatch.Groups["name"].Value.Trim(), prefixedClientNumber);
        }

        return new ClientLookup(trimmed, null);
    }
}

public class ConvertSubmissionInput
{
    [Required]
    [Display(Name = "Client name")]
    public string ClientName { get; set; } = string.Empty;

    [Display(Name = "Reuse existing client")]
    public bool UseExistingClient { get; set; }

    [Display(Name = "Existing client")]
    public Guid? ExistingClientId { get; set; }

    [Display(Name = "Client status")]
    public string ClientStatus { get; set; } = "Active";

    [Display(Name = "Primary contact")]
    public string? PrimaryContact { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public string? Phone { get; set; }

    [Display(Name = "Client notes")]
    public string? ClientNotes { get; set; }

    [Display(Name = "Matter name")]
    public string MatterName { get; set; } = string.Empty;

    [Display(Name = "Matter status")]
    public string MatterStatus { get; set; } = "Open";

    [Display(Name = "Practice area")]
    public string? PracticeArea { get; set; }

    [Display(Name = "Opened date")]
    public DateOnly? OpenedDate { get; set; }

    [Display(Name = "Responsible user")]
    public Guid? ResponsibleUserId { get; set; }

    [Display(Name = "Lead partner")]
    public Guid? LeadPartnerId { get; set; }

    [Display(Name = "Matter notes")]
    public string? MatterNotes { get; set; }
}

public sealed record ExistingClientMatch(Guid Id, int ClientNumber, string Name, string Status);

public sealed record ClientLookup(string Name, int? ClientNumber);
