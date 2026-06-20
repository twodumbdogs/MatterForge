using System.Text;
using CMIForge.Data;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Reports;

public class IndexModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    ProductPlanService productPlanService) : PageModel
{
    private const int MaxRows = 500;

    [BindProperty(SupportsGet = true)]
    public string? ReportKey { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Dataset { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<string> Fields { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Filter1Field { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Filter1Operator { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Filter1Value { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Filter2Field { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Filter2Operator { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Filter2Value { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Filter3Field { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Filter3Operator { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Filter3Value { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool RunCustom { get; set; }

    public List<BuiltInReportDefinition> BuiltInReports { get; } = BuiltInReportDefinitions.All;

    public List<DatasetDefinition> Datasets { get; } = DatasetDefinitions.All;

    public BuiltInReportDefinition? SelectedBuiltInReport { get; private set; }

    public DatasetDefinition SelectedDataset { get; private set; } = DatasetDefinitions.All[0];

    public ReportResult? BuiltInResult { get; private set; }

    public ReportResult? CustomResult { get; private set; }

    public List<SelectListItem> DatasetOptions => Datasets
        .Select(x => new SelectListItem(x.Name, x.Key, x.Key == SelectedDataset.Key))
        .ToList();

    public List<SelectListItem> OperatorOptions { get; } =
    [
        new("Contains", ReportFilterOperators.Contains),
        new("Equals", ReportFilterOperators.EqualTo),
        new("Does not equal", ReportFilterOperators.NotEquals),
        new("Starts with", ReportFilterOperators.StartsWith),
        new("Is blank", ReportFilterOperators.IsBlank),
        new("Is not blank", ReportFilterOperators.IsNotBlank)
    ];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await CanViewReportsAsync())
        {
            return Forbid();
        }

        NormalizeSelection();

        if (!string.IsNullOrWhiteSpace(ReportKey))
        {
            SelectedBuiltInReport = BuiltInReports.FirstOrDefault(x => x.Key.Equals(ReportKey, StringComparison.OrdinalIgnoreCase));
            if (SelectedBuiltInReport is not null)
            {
                BuiltInResult = await BuildReportAsync(SelectedBuiltInReport.DatasetKey, SelectedBuiltInReport.Fields, SelectedBuiltInReport.Filters);
                BuiltInResult = BuiltInResult with { Title = SelectedBuiltInReport.Name, Description = SelectedBuiltInReport.Description };
            }
        }

        if (RunCustom)
        {
            var filters = BuildCustomFilters();
            CustomResult = await BuildReportAsync(SelectedDataset.Key, Fields, filters);
            CustomResult = CustomResult with
            {
                Title = $"Custom {SelectedDataset.Name} Report",
                Description = "Ad hoc report built from selected fields and filters."
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        if (!await CanViewReportsAsync())
        {
            return Forbid();
        }

        NormalizeSelection();

        ReportResult? result = null;
        var fileName = "cmiforge-report.csv";
        if (!string.IsNullOrWhiteSpace(ReportKey))
        {
            var report = BuiltInReports.FirstOrDefault(x => x.Key.Equals(ReportKey, StringComparison.OrdinalIgnoreCase));
            if (report is not null)
            {
                result = await BuildReportAsync(report.DatasetKey, report.Fields, report.Filters);
                fileName = $"cmiforge-report-{report.Key}.csv";
            }
        }
        else if (RunCustom)
        {
            result = await BuildReportAsync(SelectedDataset.Key, Fields, BuildCustomFilters());
            fileName = $"cmiforge-report-custom-{SelectedDataset.Key}.csv";
        }

        if (result is null)
        {
            return RedirectToPage();
        }

        return File(Encoding.UTF8.GetBytes(ToCsv(result)), "text/csv", fileName);
    }

    private async Task<bool> CanViewReportsAsync()
    {
        return productPlanService.AllowsFeature(ProductFeatureKeys.Reporting) &&
            await permissionService.HasAsync(PermissionKeys.ReportingView);
    }

    private void NormalizeSelection()
    {
        SelectedDataset = Datasets.FirstOrDefault(x => x.Key.Equals(Dataset ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            ?? Datasets[0];

        var allowedFields = SelectedDataset.Fields.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Fields = Fields
            .Where(x => allowedFields.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (Fields.Count == 0)
        {
            Fields = SelectedDataset.DefaultFieldKeys.ToList();
        }
    }

    private List<ReportFilter> BuildCustomFilters()
    {
        var filters = new List<ReportFilter>();
        AddFilter(filters, Filter1Field, Filter1Operator, Filter1Value);
        AddFilter(filters, Filter2Field, Filter2Operator, Filter2Value);
        AddFilter(filters, Filter3Field, Filter3Operator, Filter3Value);
        return filters;
    }

    private void AddFilter(List<ReportFilter> filters, string? field, string? op, string? value)
    {
        if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(op))
        {
            return;
        }

        var isBlankOperator = op is ReportFilterOperators.IsBlank or ReportFilterOperators.IsNotBlank;
        if (!isBlankOperator && string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!SelectedDataset.Fields.Any(x => x.Key.Equals(field, StringComparison.OrdinalIgnoreCase)) ||
            !ReportFilterOperators.All.Contains(op))
        {
            return;
        }

        filters.Add(new ReportFilter(field, op, value?.Trim() ?? string.Empty));
    }

    private async Task<ReportResult> BuildReportAsync(string datasetKey, IReadOnlyList<string> fieldKeys, IReadOnlyList<ReportFilter> filters)
    {
        var dataset = Datasets.First(x => x.Key == datasetKey);
        var rows = await LoadRowsAsync(datasetKey);
        foreach (var filter in filters)
        {
            rows = rows.Where(row => MatchesFilter(row, filter)).ToList();
        }

        var columns = fieldKeys
            .Select(key => dataset.Fields.FirstOrDefault(field => field.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            .Where(field => field is not null)
            .Cast<ReportField>()
            .ToList();

        if (columns.Count == 0)
        {
            columns = dataset.Fields
                .Where(x => dataset.DefaultFieldKeys.Contains(x.Key))
                .ToList();
        }

        var orderedRows = rows
            .Take(MaxRows)
            .ToList();

        return new ReportResult(
            dataset.Name,
            $"Showing up to {MaxRows:N0} rows.",
            columns,
            orderedRows,
            rows.Count,
            rows.Count > MaxRows);
    }

    private async Task<List<ReportRow>> LoadRowsAsync(string datasetKey)
    {
        return datasetKey switch
        {
            DatasetKeys.Submissions => await LoadSubmissionsAsync(),
            DatasetKeys.WorkflowTasks => await LoadWorkflowTasksAsync(),
            DatasetKeys.Conflicts => await LoadConflictsAsync(),
            DatasetKeys.Matters => await LoadMattersAsync(),
            DatasetKeys.TimeEntries => await LoadTimeEntriesAsync(),
            DatasetKeys.Clients => await LoadClientsAsync(),
            DatasetKeys.Contacts => await LoadContactsAsync(),
            _ => []
        };
    }

    private async Task<List<ReportRow>> LoadSubmissionsAsync()
    {
        var submissions = await db.FormSubmissions
            .Include(x => x.FormDefinition)
            .Include(x => x.SubmitterUser)
            .Include(x => x.Client)
            .Include(x => x.Matter)
            .OrderByDescending(x => x.SubmittedAt)
            .Take(2000)
            .ToListAsync();

        return submissions.Select(x => new ReportRow(new(StringComparer.OrdinalIgnoreCase)
        {
            ["SubmissionNumber"] = RecordNumbers.Submission(x.SubmissionNumber),
            ["Form"] = x.FormDefinition?.Name ?? string.Empty,
            ["Status"] = x.Status,
            ["SubmittedBy"] = x.SubmitterUser?.DisplayName ?? x.SubmitterName,
            ["SubmittedAt"] = x.SubmittedAt.ToString("yyyy-MM-dd HH:mm"),
            ["Client"] = x.Client?.Name ?? string.Empty,
            ["Matter"] = x.Matter?.Name ?? string.Empty
        })).ToList();
    }

    private async Task<List<ReportRow>> LoadWorkflowTasksAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var tasks = await db.SubmissionWorkflowTasks
            .Include(x => x.FormSubmission)
                .ThenInclude(x => x!.FormDefinition)
            .Include(x => x.SubmissionWorkflowInstance)
                .ThenInclude(x => x!.WorkflowDefinition)
            .Include(x => x.WorkflowStep)
            .Include(x => x.AssignedUser)
            .Include(x => x.AssignedTeam)
            .OrderBy(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync();

        return tasks.Select(x => new ReportRow(new(StringComparer.OrdinalIgnoreCase)
        {
            ["SubmissionNumber"] = x.FormSubmission is null ? string.Empty : RecordNumbers.Submission(x.FormSubmission.SubmissionNumber),
            ["Form"] = x.FormSubmission?.FormDefinition?.Name ?? string.Empty,
            ["Workflow"] = x.SubmissionWorkflowInstance?.WorkflowDefinition?.Name ?? string.Empty,
            ["Step"] = x.WorkflowStep?.Name ?? string.Empty,
            ["Status"] = x.Status,
            ["AssignedTo"] = x.AssignedUser?.DisplayName ?? x.AssignedTeam?.Name ?? "Unassigned",
            ["CreatedAt"] = x.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            ["AgeDays"] = (now - x.CreatedAt).TotalDays.ToString("0.0")
        })).ToList();
    }

    private async Task<List<ReportRow>> LoadConflictsAsync()
    {
        var searches = await db.ConflictSearches
            .Include(x => x.Matter)
                .ThenInclude(x => x!.Client)
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewedByUser)
            .Include(x => x.Results)
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync();

        return searches.Select(x => new ReportRow(new(StringComparer.OrdinalIgnoreCase)
        {
            ["SearchNumber"] = RecordNumbers.ConflictSearch(x.SearchNumber),
            ["SearchName"] = x.SearchName,
            ["SearchTerms"] = x.SearchTerms,
            ["Status"] = x.Status,
            ["Decision"] = x.ReviewerDecision,
            ["Matter"] = x.Matter?.Name ?? string.Empty,
            ["Client"] = x.Matter?.Client?.Name ?? string.Empty,
            ["RequestedBy"] = x.RequestedByUser?.DisplayName ?? string.Empty,
            ["ReviewedBy"] = x.ReviewedByUser?.DisplayName ?? string.Empty,
            ["CreatedAt"] = x.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            ["Results"] = x.Results.Count.ToString()
        })).ToList();
    }

    private async Task<List<ReportRow>> LoadMattersAsync()
    {
        var matters = await db.Matters
            .Include(x => x.Client)
            .Include(x => x.ResponsibleUser)
            .Include(x => x.Parties)
            .Include(x => x.Contacts)
            .OrderBy(x => x.MatterNumber)
            .Take(2000)
            .ToListAsync();

        return matters.Select(x => new ReportRow(new(StringComparer.OrdinalIgnoreCase)
        {
            ["MatterNumber"] = x.MatterNumber.ToString("D8"),
            ["MatterName"] = x.Name,
            ["ClientNumber"] = x.Client?.ClientNumber.ToString("D8") ?? string.Empty,
            ["ClientName"] = x.Client?.Name ?? string.Empty,
            ["PracticeArea"] = x.PracticeArea,
            ["Status"] = x.Status,
            ["ResponsibleUser"] = x.ResponsibleUser?.DisplayName ?? string.Empty,
            ["OpenedDate"] = x.OpenedDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ["PartyCount"] = x.Parties.Count.ToString(),
            ["ContactCount"] = x.Contacts.Count.ToString()
        })).ToList();
    }

    private async Task<List<ReportRow>> LoadTimeEntriesAsync()
    {
        var entries = await db.TimeEntries
            .Include(x => x.User)
            .Include(x => x.Client)
            .Include(x => x.Matter)
            .Include(x => x.TimePhase)
            .Include(x => x.TimeTask)
            .OrderByDescending(x => x.WorkDate)
            .Take(2000)
            .ToListAsync();

        return entries.Select(x => new ReportRow(new(StringComparer.OrdinalIgnoreCase)
        {
            ["TimeEntryNumber"] = x.TimeEntryNumber.ToString("D8"),
            ["WorkDate"] = x.WorkDate.ToString("yyyy-MM-dd"),
            ["User"] = x.User?.DisplayName ?? string.Empty,
            ["Client"] = x.Client?.Name ?? string.Empty,
            ["Matter"] = x.Matter?.Name ?? string.Empty,
            ["Phase"] = x.TimePhase is null ? string.Empty : $"{x.TimePhase.Code} - {x.TimePhase.Name}",
            ["Task"] = x.TimeTask is null ? string.Empty : $"{x.TimeTask.Code} - {x.TimeTask.Name}",
            ["Hours"] = (x.Minutes / 60m).ToString("0.00"),
            ["Billable"] = x.IsBillable ? "Yes" : "No",
            ["Status"] = x.Status,
            ["Exported"] = x.ExportedAt.HasValue ? "Yes" : "No",
            ["ClientNarrative"] = x.ClientNarrative,
            ["InternalNotes"] = x.InternalNotes
        })).ToList();
    }

    private async Task<List<ReportRow>> LoadClientsAsync()
    {
        var clients = await db.Clients
            .Include(x => x.Matters)
            .Include(x => x.Contacts)
            .OrderBy(x => x.ClientNumber)
            .Take(2000)
            .ToListAsync();

        return clients.Select(x => new ReportRow(new(StringComparer.OrdinalIgnoreCase)
        {
            ["ClientNumber"] = x.ClientNumber.ToString("D8"),
            ["ClientName"] = x.Name,
            ["Status"] = x.Status,
            ["PrimaryContact"] = x.PrimaryContact,
            ["Email"] = x.Email,
            ["Phone"] = x.Phone,
            ["City"] = x.City,
            ["State"] = x.State,
            ["MatterCount"] = x.Matters.Count.ToString(),
            ["ContactCount"] = x.Contacts.Count.ToString()
        })).ToList();
    }

    private async Task<List<ReportRow>> LoadContactsAsync()
    {
        var contacts = await db.Contacts
            .Include(x => x.ClientLinks)
            .Include(x => x.MatterLinks)
            .OrderBy(x => x.ContactNumber)
            .Take(2000)
            .ToListAsync();

        return contacts.Select(x => new ReportRow(new(StringComparer.OrdinalIgnoreCase)
        {
            ["ContactNumber"] = x.ContactNumber.ToString("D8"),
            ["Name"] = x.DisplayName,
            ["Organization"] = x.Organization,
            ["Title"] = x.Title,
            ["Email"] = x.Email,
            ["Phone"] = x.Phone,
            ["MobilePhone"] = x.MobilePhone,
            ["City"] = x.City,
            ["State"] = x.State,
            ["ClientLinks"] = x.ClientLinks.Count.ToString(),
            ["MatterLinks"] = x.MatterLinks.Count.ToString()
        })).ToList();
    }

    private static bool MatchesFilter(ReportRow row, ReportFilter filter)
    {
        var value = row.Values.TryGetValue(filter.FieldKey, out var rawValue)
            ? rawValue ?? string.Empty
            : string.Empty;
        var comparisonValue = filter.Value ?? string.Empty;

        return filter.Operator switch
        {
            ReportFilterOperators.EqualTo => value.Equals(comparisonValue, StringComparison.OrdinalIgnoreCase),
            ReportFilterOperators.NotEquals => !value.Equals(comparisonValue, StringComparison.OrdinalIgnoreCase),
            ReportFilterOperators.StartsWith => value.StartsWith(comparisonValue, StringComparison.OrdinalIgnoreCase),
            ReportFilterOperators.IsBlank => string.IsNullOrWhiteSpace(value),
            ReportFilterOperators.IsNotBlank => !string.IsNullOrWhiteSpace(value),
            _ => value.Contains(comparisonValue, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string ToCsv(ReportResult result)
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", result.Columns.Select(x => Csv(x.Label))));
        foreach (var row in result.Rows)
        {
            csv.AppendLine(string.Join(",", result.Columns.Select(column =>
                Csv(row.Values.TryGetValue(column.Key, out var value) ? value : string.Empty))));
        }

        return csv.ToString();
    }

    private static string Csv(string? value)
    {
        var safeValue = value ?? string.Empty;
        return "\"" + safeValue.Replace("\"", "\"\"") + "\"";
    }
}

public static class DatasetKeys
{
    public const string Submissions = "submissions";
    public const string WorkflowTasks = "workflow-tasks";
    public const string Conflicts = "conflicts";
    public const string Matters = "matters";
    public const string TimeEntries = "time-entries";
    public const string Clients = "clients";
    public const string Contacts = "contacts";
}

public static class ReportFilterOperators
{
    public const string Contains = "contains";
    public const string EqualTo = "equals";
    public const string NotEquals = "not-equals";
    public const string StartsWith = "starts-with";
    public const string IsBlank = "is-blank";
    public const string IsNotBlank = "is-not-blank";

    public static readonly string[] All = [Contains, EqualTo, NotEquals, StartsWith, IsBlank, IsNotBlank];
}

public sealed record ReportField(string Key, string Label);

public sealed record ReportFilter(string FieldKey, string Operator, string? Value);

public sealed record ReportRow(Dictionary<string, string> Values);

public sealed record ReportResult(
    string Title,
    string Description,
    IReadOnlyList<ReportField> Columns,
    IReadOnlyList<ReportRow> Rows,
    int TotalRows,
    bool IsTruncated);

public sealed record DatasetDefinition(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<ReportField> Fields,
    IReadOnlyList<string> DefaultFieldKeys);

public sealed record BuiltInReportDefinition(
    string Key,
    string Name,
    string Description,
    string DatasetKey,
    IReadOnlyList<string> Fields,
    IReadOnlyList<ReportFilter> Filters);

public static class BuiltInReportDefinitions
{
    public static readonly List<BuiltInReportDefinition> All =
    [
        new(
            "intake-pipeline",
            "Intake Pipeline",
            "Submitted intake requests with status, submitter, client, and matter context.",
            DatasetKeys.Submissions,
            ["SubmissionNumber", "Form", "Status", "SubmittedBy", "SubmittedAt", "Client", "Matter"],
            []),
        new(
            "approval-queue-aging",
            "Approval Queue Aging",
            "Open workflow tasks, assignment, and age in days.",
            DatasetKeys.WorkflowTasks,
            ["SubmissionNumber", "Workflow", "Step", "AssignedTo", "Status", "CreatedAt", "AgeDays"],
            [new("Status", ReportFilterOperators.EqualTo, WorkflowStatuses.TaskOpen)]),
        new(
            "conflicts-review",
            "Conflicts Review",
            "Conflict searches with decision, matter context, reviewer, and result count.",
            DatasetKeys.Conflicts,
            ["SearchNumber", "SearchName", "Status", "Decision", "Matter", "Client", "RequestedBy", "ReviewedBy", "Results"],
            []),
        new(
            "matter-roster",
            "Matter Roster",
            "Operational matter list by client, status, practice area, responsible user, parties, and contacts.",
            DatasetKeys.Matters,
            ["MatterNumber", "MatterName", "ClientName", "PracticeArea", "Status", "ResponsibleUser", "OpenedDate", "PartyCount", "ContactCount"],
            []),
        new(
            "time-detail",
            "Time Detail",
            "Time entries by user, client, matter, codes, hours, billable flag, and narrative.",
            DatasetKeys.TimeEntries,
            ["TimeEntryNumber", "WorkDate", "User", "Client", "Matter", "Phase", "Task", "Hours", "Billable", "Status", "ClientNarrative"],
            [])
    ];
}

public static class DatasetDefinitions
{
    public static readonly List<DatasetDefinition> All =
    [
        new(
            DatasetKeys.Submissions,
            "Submissions",
            "Form submissions and conversion context.",
            [
                new("SubmissionNumber", "Submission #"),
                new("Form", "Form"),
                new("Status", "Status"),
                new("SubmittedBy", "Submitted by"),
                new("SubmittedAt", "Submitted at"),
                new("Client", "Client"),
                new("Matter", "Matter")
            ],
            ["SubmissionNumber", "Form", "Status", "SubmittedBy", "SubmittedAt"]),
        new(
            DatasetKeys.WorkflowTasks,
            "Workflow Tasks",
            "Workflow queue and task aging data.",
            [
                new("SubmissionNumber", "Submission #"),
                new("Form", "Form"),
                new("Workflow", "Workflow"),
                new("Step", "Step"),
                new("Status", "Status"),
                new("AssignedTo", "Assigned to"),
                new("CreatedAt", "Created at"),
                new("AgeDays", "Age days")
            ],
            ["SubmissionNumber", "Workflow", "Step", "AssignedTo", "Status", "AgeDays"]),
        new(
            DatasetKeys.Conflicts,
            "Conflicts",
            "Conflict searches, review decisions, and matter context.",
            [
                new("SearchNumber", "Search #"),
                new("SearchName", "Search name"),
                new("SearchTerms", "Terms"),
                new("Status", "Status"),
                new("Decision", "Decision"),
                new("Matter", "Matter"),
                new("Client", "Client"),
                new("RequestedBy", "Requested by"),
                new("ReviewedBy", "Reviewed by"),
                new("CreatedAt", "Created at"),
                new("Results", "Results")
            ],
            ["SearchNumber", "SearchName", "Status", "Decision", "Matter", "Results"]),
        new(
            DatasetKeys.Matters,
            "Matters",
            "Matter roster with client and operational context.",
            [
                new("MatterNumber", "Matter #"),
                new("MatterName", "Matter"),
                new("ClientNumber", "Client #"),
                new("ClientName", "Client"),
                new("PracticeArea", "Practice area"),
                new("Status", "Status"),
                new("ResponsibleUser", "Responsible user"),
                new("OpenedDate", "Opened date"),
                new("PartyCount", "Parties"),
                new("ContactCount", "Contacts")
            ],
            ["MatterNumber", "MatterName", "ClientName", "PracticeArea", "Status", "ResponsibleUser"]),
        new(
            DatasetKeys.TimeEntries,
            "Time Entries",
            "Time records for operational reporting.",
            [
                new("TimeEntryNumber", "Time #"),
                new("WorkDate", "Work date"),
                new("User", "User"),
                new("Client", "Client"),
                new("Matter", "Matter"),
                new("Phase", "Phase"),
                new("Task", "Task"),
                new("Hours", "Hours"),
                new("Billable", "Billable"),
                new("Status", "Status"),
                new("Exported", "Exported"),
                new("ClientNarrative", "Client narrative"),
                new("InternalNotes", "Internal notes")
            ],
            ["TimeEntryNumber", "WorkDate", "User", "Matter", "Hours", "Billable"]),
        new(
            DatasetKeys.Clients,
            "Clients",
            "Client list and lightweight counts.",
            [
                new("ClientNumber", "Client #"),
                new("ClientName", "Client"),
                new("Status", "Status"),
                new("PrimaryContact", "Primary contact"),
                new("Email", "Email"),
                new("Phone", "Phone"),
                new("City", "City"),
                new("State", "State"),
                new("MatterCount", "Matters"),
                new("ContactCount", "Contacts")
            ],
            ["ClientNumber", "ClientName", "Status", "PrimaryContact", "MatterCount", "ContactCount"]),
        new(
            DatasetKeys.Contacts,
            "Contacts",
            "Address-book contacts linked to clients and matters.",
            [
                new("ContactNumber", "Contact #"),
                new("Name", "Name"),
                new("Organization", "Organization"),
                new("Title", "Title"),
                new("Email", "Email"),
                new("Phone", "Phone"),
                new("MobilePhone", "Mobile"),
                new("City", "City"),
                new("State", "State"),
                new("ClientLinks", "Client links"),
                new("MatterLinks", "Matter links")
            ],
            ["ContactNumber", "Name", "Organization", "Email", "Phone", "ClientLinks", "MatterLinks"])
    ];
}
