using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CMIForge.Pages;

public class IndexModel(CMIForgeDbContext db, ProductPlanService productPlanService) : PageModel
{
    public int FormCount { get; private set; }

    public int SubmissionCount { get; private set; }

    public int PublishedVersionCount { get; private set; }

    public int TimeEntryCount { get; private set; }

    public decimal TimeHours { get; private set; }

    public List<FormDefinition> LatestForms { get; private set; } = [];

    public ProductPlan CurrentPlan => productPlanService.CurrentPlan;

    public ProductUsageSnapshot Usage { get; private set; } = new(0, 0, 0);

    public List<DashboardSlice> SubmissionStatusSlices { get; private set; } = [];

    public string SubmissionStatusGradient { get; private set; } = "#e5ebf2";

    public List<DashboardSlice> ConflictStatusSlices { get; private set; } = [];

    public string ConflictStatusGradient { get; private set; } = "#e5ebf2";

    public List<DashboardBarPoint> RecentSubmissionBars { get; private set; } = [];

    public List<DashboardBarPoint> WorkflowTaskBars { get; private set; } = [];

    public int OpenWorkflowTaskCount { get; private set; }

    public int ConflictSearchCount { get; private set; }

    public async Task OnGetAsync()
    {
        FormCount = await db.FormDefinitions.AsNoTracking().CountAsync(x => x.IsActive);
        SubmissionCount = await db.FormSubmissions.AsNoTracking().CountAsync();
        PublishedVersionCount = await db.FormVersions.AsNoTracking().CountAsync(x => x.IsPublished);
        TimeEntryCount = await db.TimeEntries.AsNoTracking().CountAsync();
        TimeHours = await db.TimeEntries.AsNoTracking().SumAsync(x => (decimal?)x.Minutes) / 60m ?? 0m;
        Usage = await productPlanService.GetUsageAsync();
        OpenWorkflowTaskCount = await db.SubmissionWorkflowTasks.AsNoTracking().CountAsync(x => x.Status == WorkflowStatuses.TaskOpen);
        ConflictSearchCount = await db.ConflictSearches.AsNoTracking().CountAsync();
        LatestForms = await db.FormDefinitions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .ToListAsync();

        var submissionStatusRows = await db.FormSubmissions
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(x => new { Label = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var submissionStatuses = submissionStatusRows
            .Select(x => new CountBucket(x.Label, x.Count))
            .ToList();
        SubmissionStatusSlices = BuildSlices(submissionStatuses, ["#255ea8", "#127a69", "#b7791f", "#7c3aed", "#dc2626", "#647084"]);
        SubmissionStatusGradient = BuildConicGradient(SubmissionStatusSlices);

        var conflictStatusRows = await db.ConflictSearches
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(x => new { Label = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var conflictStatuses = conflictStatusRows
            .Select(x => new CountBucket(x.Label, x.Count))
            .ToList();
        ConflictStatusSlices = BuildSlices(conflictStatuses, ["#127a69", "#b7791f", "#dc2626", "#255ea8", "#647084"]);
        ConflictStatusGradient = BuildConicGradient(ConflictStatusSlices);

        var taskRows = await db.SubmissionWorkflowTasks
            .AsNoTracking()
            .Where(x => x.Status == WorkflowStatuses.TaskOpen)
            .GroupBy(x => x.WorkflowStep!.Name)
            .Select(x => new { Label = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToListAsync();
        var taskBuckets = taskRows
            .Select(x => new CountBucket(x.Label, x.Count))
            .ToList();
        WorkflowTaskBars = BuildBars(taskBuckets);

        var startDate = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date.AddDays(-13), TimeSpan.Zero);
        var recentSubmissions = await db.FormSubmissions
            .AsNoTracking()
            .Where(x => x.SubmittedAt >= startDate)
            .Select(x => x.SubmittedAt)
            .ToListAsync();
        var recentCounts = Enumerable.Range(0, 14)
            .Select(offset =>
            {
                var date = startDate.AddDays(offset);
                var count = recentSubmissions.Count(x => x.UtcDateTime.Date == date.UtcDateTime.Date);
                return new CountBucket(date.ToString("MMM d", CultureInfo.InvariantCulture), count);
            })
            .ToList();
        RecentSubmissionBars = BuildBars(recentCounts);
    }

    public decimal GetUsagePercent(int currentCount, int? limit)
    {
        if (!limit.HasValue || limit.Value <= 0)
        {
            return 100m;
        }

        return Math.Clamp(currentCount / (decimal)limit.Value * 100m, 0m, 100m);
    }

    public string FormatPercent(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static List<DashboardSlice> BuildSlices(IReadOnlyList<CountBucket> buckets, string[] colors)
    {
        var total = buckets.Sum(x => x.Count);
        if (total == 0)
        {
            return [];
        }

        return buckets
            .Select((bucket, index) => new DashboardSlice(
                bucket.Label,
                bucket.Count,
                bucket.Count / (decimal)total * 100m,
                colors[index % colors.Length]))
            .ToList();
    }

    private static string BuildConicGradient(IReadOnlyList<DashboardSlice> slices)
    {
        if (slices.Count == 0)
        {
            return "#e5ebf2";
        }

        var cursor = 0m;
        var parts = new List<string>();
        foreach (var slice in slices)
        {
            var start = cursor;
            cursor += slice.Percent;
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{slice.Color} {start:0.##}% {cursor:0.##}%"));
        }

        return $"conic-gradient({string.Join(", ", parts)})";
    }

    private static List<DashboardBarPoint> BuildBars(IReadOnlyList<CountBucket> buckets)
    {
        var max = buckets.Count == 0 ? 0 : buckets.Max(x => x.Count);
        return buckets
            .Select(bucket => new DashboardBarPoint(
                bucket.Label,
                bucket.Count,
                max == 0 ? 0m : bucket.Count / (decimal)max * 100m))
            .ToList();
    }
}

public sealed record DashboardSlice(string Label, int Count, decimal Percent, string Color);

public sealed record DashboardBarPoint(string Label, int Count, decimal Percent);

internal sealed record CountBucket(string Label, int Count);
