using System.Text;
using MatterForge.Data;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Reports;

public class IndexModel(
    MatterForgeDbContext db,
    PermissionService permissionService,
    ProductPlanService productPlanService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? To { get; set; }

    public List<CountRow> SubmissionStatuses { get; private set; } = [];

    public List<CountRow> ConflictStatuses { get; private set; } = [];

    public List<CountRow> MatterPracticeAreas { get; private set; } = [];

    public List<MonthCountRow> NewMattersByMonth { get; private set; } = [];

    public List<TimeByUserRow> TimeByUser { get; private set; } = [];

    public List<TimeByMatterRow> TimeByMatter { get; private set; } = [];

    public int OpenWorkflowTasks { get; private set; }

    public decimal AverageWorkflowAgeDays { get; private set; }

    public string OldestWorkflowTask { get; private set; } = "None";

    public int TimeEntryCount { get; private set; }

    public decimal TotalHours { get; private set; }

    public decimal BillableHours { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await CanViewReportsAsync())
        {
            return Forbid();
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnGetTimeCsvAsync()
    {
        if (!await CanViewReportsAsync())
        {
            return Forbid();
        }

        NormalizeDates();
        var entries = await db.TimeEntries
            .Include(x => x.User)
            .Include(x => x.Client)
            .Include(x => x.Matter)
            .Where(x => x.WorkDate >= From!.Value && x.WorkDate <= To!.Value)
            .OrderByDescending(x => x.WorkDate)
            .ThenBy(x => x.User!.DisplayName)
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("TimeEntryNumber,WorkDate,User,Client,Matter,Hours,Billable,Status,Narrative");
        foreach (var entry in entries)
        {
            csv.AppendLine(string.Join(",",
                entry.TimeEntryNumber.ToString("D8"),
                entry.WorkDate.ToString("yyyy-MM-dd"),
                Csv(entry.User?.DisplayName),
                Csv(entry.Client?.Name),
                Csv(entry.Matter?.Name),
                (entry.Minutes / 60m).ToString("0.00"),
                entry.IsBillable ? "Yes" : "No",
                Csv(entry.Status),
                Csv(entry.Narrative)));
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "cmiforge-report-time-detail.csv");
    }

    private async Task<bool> CanViewReportsAsync()
    {
        return productPlanService.AllowsFeature(ProductFeatureKeys.Reporting) &&
            await permissionService.HasAsync(PermissionKeys.ReportingView);
    }

    private async Task LoadAsync()
    {
        NormalizeDates();

        var submissionStatusRows = await db.FormSubmissions
            .GroupBy(x => x.Status)
            .Select(x => new { Label = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        SubmissionStatuses = submissionStatusRows
            .Select(x => new CountRow(x.Label, x.Count))
            .ToList();

        var conflictStatusRows = await db.ConflictSearches
            .GroupBy(x => x.ReviewerDecision == null || x.ReviewerDecision == string.Empty ? x.Status : x.Status + " / " + x.ReviewerDecision)
            .Select(x => new { Label = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToListAsync();
        ConflictStatuses = conflictStatusRows
            .Select(x => new CountRow(x.Label, x.Count))
            .ToList();

        var matterPracticeAreaRows = await db.Matters
            .GroupBy(x => x.PracticeArea == null || x.PracticeArea == string.Empty ? "Unspecified" : x.PracticeArea)
            .Select(x => new { Label = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label)
            .Take(8)
            .ToListAsync();
        MatterPracticeAreas = matterPracticeAreaRows
            .Select(x => new CountRow(x.Label, x.Count))
            .ToList();

        var firstMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
        var matters = await db.Matters
            .Where(x => x.OpenedDate == null || x.OpenedDate >= firstMonth)
            .Select(x => new { x.OpenedDate, x.CreatedAt })
            .ToListAsync();
        NewMattersByMonth = Enumerable.Range(0, 6)
            .Select(i => firstMonth.AddMonths(i))
            .Select(month =>
            {
                var count = matters.Count(x =>
                {
                    var opened = x.OpenedDate ?? DateOnly.FromDateTime(x.CreatedAt.LocalDateTime);
                    return opened.Year == month.Year && opened.Month == month.Month;
                });
                return new MonthCountRow(month.ToString("MMM yyyy"), count);
            })
            .ToList();

        var openTasks = await db.SubmissionWorkflowTasks
            .Include(x => x.WorkflowStep)
            .Include(x => x.FormSubmission)
            .Where(x => x.Status == WorkflowStatuses.TaskOpen)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
        OpenWorkflowTasks = openTasks.Count;
        if (openTasks.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            AverageWorkflowAgeDays = openTasks.Average(x => (decimal)(now - x.CreatedAt).TotalDays);
            var oldest = openTasks[0];
            OldestWorkflowTask = $"{oldest.WorkflowStep?.Name ?? "Task"} for submission {oldest.FormSubmission?.SubmissionNumber.ToString("D8") ?? "unknown"}";
        }

        var timeEntries = await db.TimeEntries
            .Include(x => x.User)
            .Include(x => x.Client)
            .Include(x => x.Matter)
            .Where(x => x.WorkDate >= From!.Value && x.WorkDate <= To!.Value)
            .ToListAsync();

        TimeEntryCount = timeEntries.Count;
        TotalHours = timeEntries.Sum(x => x.Minutes) / 60m;
        BillableHours = timeEntries.Where(x => x.IsBillable).Sum(x => x.Minutes) / 60m;

        TimeByUser = timeEntries
            .GroupBy(x => x.User?.DisplayName ?? "Unknown")
            .Select(x => new TimeByUserRow(
                x.Key,
                x.Sum(y => y.Minutes) / 60m,
                x.Where(y => y.IsBillable).Sum(y => y.Minutes) / 60m,
                x.Count()))
            .OrderByDescending(x => x.Hours)
            .Take(10)
            .ToList();

        TimeByMatter = timeEntries
            .GroupBy(x => new { Client = x.Client?.Name ?? "Unknown", Matter = x.Matter?.Name ?? "Unknown" })
            .Select(x => new TimeByMatterRow(
                x.Key.Client,
                x.Key.Matter,
                x.Sum(y => y.Minutes) / 60m,
                x.Where(y => y.IsBillable).Sum(y => y.Minutes) / 60m))
            .OrderByDescending(x => x.Hours)
            .Take(10)
            .ToList();
    }

    private void NormalizeDates()
    {
        To ??= DateOnly.FromDateTime(DateTime.Today);
        From ??= To.Value.AddDays(-30);
    }

    private static string Csv(string? value)
    {
        var safeValue = value ?? string.Empty;
        return "\"" + safeValue.Replace("\"", "\"\"") + "\"";
    }
}

public sealed record CountRow(string Label, int Count);

public sealed record MonthCountRow(string Month, int Count);

public sealed record TimeByUserRow(string UserName, decimal Hours, decimal BillableHours, int EntryCount);

public sealed record TimeByMatterRow(string ClientName, string MatterName, decimal Hours, decimal BillableHours);
