using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CMIForge.Pages;

public class IndexModel(
    CMIForgeDbContext db,
    ProductPlanService productPlanService,
    CurrentUserService currentUserService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string View { get; set; } = DashboardViews.Firm;

    public CMIForgeUser? CurrentUser { get; private set; }

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

    public int MyOpenTaskCount { get; private set; }

    public int MyTeamTaskCount { get; private set; }

    public int MySubmissionCount { get; private set; }

    public int MyDraftTimeCount { get; private set; }

    public int MySubmittedTimeCount { get; private set; }

    public decimal MyTimeHours { get; private set; }

    public int MyEscalationCount { get; private set; }

    public List<DashboardTaskItem> MyOpenTasks { get; private set; } = [];

    public List<DashboardSubmissionItem> MyRecentSubmissions { get; private set; } = [];

    public List<DashboardTimeItem> MyRecentTimeEntries { get; private set; } = [];

    public List<DashboardEscalationItem> MyEscalations { get; private set; } = [];

    public int PartnerLeadMatterCount { get; private set; }

    public int PartnerOpenMatterCount { get; private set; }

    public int PartnerTimeApprovalCount { get; private set; }

    public int PartnerPendingConflictCount { get; private set; }

    public int PartnerSubmissionCount { get; private set; }

    public List<DashboardMatterItem> PartnerMatters { get; private set; } = [];

    public List<DashboardTimeItem> PartnerTimeApprovals { get; private set; } = [];

    public List<DashboardEscalationItem> PartnerConflictEscalations { get; private set; } = [];

    public List<DashboardSubmissionItem> PartnerSubmissions { get; private set; } = [];

    public async Task OnGetAsync()
    {
        CurrentUser = await currentUserService.GetCurrentUserAsync();
        View = NormalizeView(View);

        if (View == DashboardViews.User)
        {
            await LoadUserDashboardAsync();
            return;
        }

        if (View == DashboardViews.Partner)
        {
            await LoadPartnerDashboardAsync();
            return;
        }

        await LoadFirmDashboardAsync();
    }

    private async Task LoadFirmDashboardAsync()
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

    private async Task LoadUserDashboardAsync()
    {
        if (CurrentUser is null)
        {
            return;
        }

        var currentUserId = CurrentUser.Id;
        var teamIds = await currentUserService.GetCurrentUserTeamIdsAsync();
        var openTaskBase = db.SubmissionWorkflowTasks
            .AsNoTracking()
            .Where(x => x.Status == WorkflowStatuses.TaskOpen);

        MyOpenTaskCount = await openTaskBase.CountAsync(x => x.AssignedUserId == currentUserId);
        MyTeamTaskCount = teamIds.Count == 0
            ? 0
            : await openTaskBase.CountAsync(x => x.AssignedTeamId.HasValue && teamIds.Contains(x.AssignedTeamId.Value));
        MySubmissionCount = await db.FormSubmissions
            .AsNoTracking()
            .CountAsync(x => x.SubmitterUserId == currentUserId && x.Status != SubmissionStatuses.Cancelled);
        MyDraftTimeCount = await db.TimeEntries
            .AsNoTracking()
            .CountAsync(x => x.UserId == currentUserId && x.Status == TimeEntryStatuses.Draft);
        MySubmittedTimeCount = await db.TimeEntries
            .AsNoTracking()
            .CountAsync(x => x.UserId == currentUserId && x.Status == TimeEntryStatuses.Submitted);
        MyTimeHours = await db.TimeEntries
            .AsNoTracking()
            .Where(x => x.UserId == currentUserId)
            .SumAsync(x => (decimal?)x.Minutes) / 60m ?? 0m;
        MyEscalationCount = await db.ConflictSearchResults
            .AsNoTracking()
            .CountAsync(x => x.EscalatedToUserId == currentUserId && !x.EscalationApprovedAt.HasValue);

        var taskQuery = openTaskBase.Where(x => x.AssignedUserId == currentUserId);
        if (teamIds.Count > 0)
        {
            taskQuery = openTaskBase.Where(x =>
                x.AssignedUserId == currentUserId ||
                (x.AssignedTeamId.HasValue && teamIds.Contains(x.AssignedTeamId.Value)));
        }

        var tasks = await taskQuery
            .Include(x => x.FormSubmission)
                .ThenInclude(x => x!.FormDefinition)
            .Include(x => x.WorkflowStep)
            .Include(x => x.AssignedUser)
            .Include(x => x.AssignedTeam)
            .Include(x => x.SubmissionWorkflowInstance)
                .ThenInclude(x => x!.WorkflowDefinition)
            .OrderBy(x => x.CreatedAt)
            .Take(6)
            .ToListAsync();
        MyOpenTasks = tasks.Select(DashboardTaskItem.FromTask).ToList();

        var submissions = await db.FormSubmissions
            .AsNoTracking()
            .Include(x => x.FormDefinition)
            .Include(x => x.Client)
            .Include(x => x.Matter)
            .Where(x => x.SubmitterUserId == currentUserId && x.Status != SubmissionStatuses.Cancelled)
            .OrderByDescending(x => x.SubmittedAt)
            .Take(5)
            .ToListAsync();
        MyRecentSubmissions = submissions.Select(DashboardSubmissionItem.FromSubmission).ToList();

        var timeEntries = await db.TimeEntries
            .AsNoTracking()
            .Include(x => x.Matter)
            .Where(x => x.UserId == currentUserId)
            .OrderByDescending(x => x.WorkDate)
            .ThenByDescending(x => x.CreatedAt)
            .Take(5)
            .ToListAsync();
        MyRecentTimeEntries = timeEntries.Select(DashboardTimeItem.FromTimeEntry).ToList();

        var escalations = await db.ConflictSearchResults
            .AsNoTracking()
            .Include(x => x.ConflictSearch)
            .Where(x => x.EscalatedToUserId == currentUserId && !x.EscalationApprovedAt.HasValue)
            .OrderByDescending(x => x.EscalatedAt)
            .Take(5)
            .ToListAsync();
        MyEscalations = escalations.Select(DashboardEscalationItem.FromResult).ToList();
    }

    private async Task LoadPartnerDashboardAsync()
    {
        if (CurrentUser is null)
        {
            return;
        }

        var currentUserId = CurrentUser.Id;
        var leadMatterIds = await db.Matters
            .AsNoTracking()
            .Where(x => !x.IsArchived && x.LeadPartnerId == currentUserId)
            .Select(x => x.Id)
            .ToListAsync();

        PartnerLeadMatterCount = leadMatterIds.Count;
        PartnerOpenMatterCount = await db.Matters
            .AsNoTracking()
            .CountAsync(x => !x.IsArchived && x.LeadPartnerId == currentUserId && x.Status == "Open");
        PartnerTimeApprovalCount = leadMatterIds.Count == 0
            ? 0
            : await db.TimeEntries
                .AsNoTracking()
                .CountAsync(x => leadMatterIds.Contains(x.MatterId) && x.Status == TimeEntryStatuses.Submitted);
        PartnerPendingConflictCount = leadMatterIds.Count == 0
            ? 0
            : await db.ConflictSearchResults
                .AsNoTracking()
                .CountAsync(x =>
                    x.MatterId.HasValue &&
                    leadMatterIds.Contains(x.MatterId.Value) &&
                    x.ClearanceStatus == ConflictSearchDecisions.Pending);
        PartnerSubmissionCount = await db.FormSubmissions
            .AsNoTracking()
            .CountAsync(x =>
                x.LeadPartnerId == currentUserId ||
                (x.MatterId.HasValue && leadMatterIds.Contains(x.MatterId.Value)));

        var matters = await db.Matters
            .AsNoTracking()
            .Include(x => x.Client)
            .Where(x => !x.IsArchived && x.LeadPartnerId == currentUserId)
            .OrderBy(x => x.MatterNumber)
            .Take(6)
            .ToListAsync();
        PartnerMatters = matters.Select(matter =>
        {
            var openTasks = db.SubmissionWorkflowTasks.Count(x =>
                x.Status == WorkflowStatuses.TaskOpen &&
                x.FormSubmission != null &&
                x.FormSubmission.MatterId == matter.Id);
            var submittedTime = db.TimeEntries.Count(x =>
                x.MatterId == matter.Id &&
                x.Status == TimeEntryStatuses.Submitted);
            return DashboardMatterItem.FromMatter(matter, openTasks, submittedTime);
        }).ToList();

        List<TimeEntry> approvals = leadMatterIds.Count == 0
            ? []
            : await db.TimeEntries
                .AsNoTracking()
                .Include(x => x.Matter)
                .Where(x => leadMatterIds.Contains(x.MatterId) && x.Status == TimeEntryStatuses.Submitted)
                .OrderBy(x => x.WorkDate)
                .ThenBy(x => x.CreatedAt)
                .Take(5)
                .ToListAsync();
        PartnerTimeApprovals = approvals.Select(DashboardTimeItem.FromTimeEntry).ToList();

        List<ConflictSearchResult> conflicts = leadMatterIds.Count == 0
            ? []
            : await db.ConflictSearchResults
                .AsNoTracking()
                .Include(x => x.ConflictSearch)
                .Where(x =>
                    x.MatterId.HasValue &&
                    leadMatterIds.Contains(x.MatterId.Value) &&
                    x.ClearanceStatus == ConflictSearchDecisions.Pending)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.CreatedAt)
                .Take(5)
                .ToListAsync();
        PartnerConflictEscalations = conflicts.Select(DashboardEscalationItem.FromResult).ToList();

        var submissions = await db.FormSubmissions
            .AsNoTracking()
            .Include(x => x.FormDefinition)
            .Include(x => x.Client)
            .Include(x => x.Matter)
            .Where(x =>
                x.LeadPartnerId == currentUserId ||
                (x.MatterId.HasValue && leadMatterIds.Contains(x.MatterId.Value)))
            .OrderByDescending(x => x.SubmittedAt)
            .Take(5)
            .ToListAsync();
        PartnerSubmissions = submissions.Select(DashboardSubmissionItem.FromSubmission).ToList();
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

    private static string NormalizeView(string? view)
    {
        return view is DashboardViews.User or DashboardViews.Partner
            ? view
            : DashboardViews.Firm;
    }
}

public sealed record DashboardSlice(string Label, int Count, decimal Percent, string Color);

public sealed record DashboardBarPoint(string Label, int Count, decimal Percent);

public sealed record DashboardTaskItem(
    Guid Id,
    Guid SubmissionId,
    int SubmissionNumber,
    string FormName,
    string WorkflowName,
    string StepName,
    string AssignedTo,
    DateTimeOffset CreatedAt)
{
    public static DashboardTaskItem FromTask(SubmissionWorkflowTask task)
    {
        return new DashboardTaskItem(
            task.Id,
            task.FormSubmissionId,
            task.FormSubmission?.SubmissionNumber ?? 0,
            task.FormSubmission?.FormDefinition?.Name ?? "Submission",
            task.SubmissionWorkflowInstance?.WorkflowDefinition?.Name ?? "Workflow",
            task.WorkflowStep?.Name ?? "Step",
            task.AssignedUser?.DisplayName ?? task.AssignedTeam?.Name ?? "Unassigned",
            task.CreatedAt);
    }
}

public sealed record DashboardSubmissionItem(
    Guid Id,
    int SubmissionNumber,
    string FormName,
    string Status,
    string ClientName,
    string MatterName,
    DateTimeOffset SubmittedAt)
{
    public static DashboardSubmissionItem FromSubmission(FormSubmission submission)
    {
        var answers = SubmissionAnswerReader.Read(submission.DataJson);
        return new DashboardSubmissionItem(
            submission.Id,
            submission.SubmissionNumber,
            submission.FormDefinition?.Name ?? "Submission",
            submission.Status,
            submission.Client?.Name ?? SubmissionAnswerReader.FirstValue(answers, "clientName", "client", "companyName"),
            submission.Matter?.Name ?? SubmissionAnswerReader.FirstValue(answers, "matterName", "matter"),
            submission.SubmittedAt);
    }
}

public sealed record DashboardTimeItem(
    Guid Id,
    int TimeEntryNumber,
    DateOnly WorkDate,
    string MatterName,
    string Status,
    int Minutes,
    bool IsBillable)
{
    public decimal Hours => Minutes / 60m;

    public static DashboardTimeItem FromTimeEntry(TimeEntry entry)
    {
        return new DashboardTimeItem(
            entry.Id,
            entry.TimeEntryNumber,
            entry.WorkDate,
            entry.Matter?.Name ?? "Matter",
            entry.Status,
            entry.Minutes,
            entry.IsBillable);
    }
}

public sealed record DashboardMatterItem(
    Guid Id,
    int MatterNumber,
    string Name,
    string ClientName,
    string Status,
    int OpenWorkflowTasks,
    int SubmittedTimeCount)
{
    public static DashboardMatterItem FromMatter(Matter matter, int openWorkflowTasks, int submittedTimeCount)
    {
        return new DashboardMatterItem(
            matter.Id,
            matter.MatterNumber,
            matter.Name,
            matter.Client?.Name ?? "Client",
            matter.Status,
            openWorkflowTasks,
            submittedTimeCount);
    }
}

public sealed record DashboardEscalationItem(
    Guid SearchId,
    Guid ResultId,
    int SearchNumber,
    string SearchName,
    string MatchedName,
    string RiskLevel,
    int Score,
    DateTimeOffset? EscalatedAt,
    string Notes)
{
    public static DashboardEscalationItem FromResult(ConflictSearchResult result)
    {
        return new DashboardEscalationItem(
            result.ConflictSearchId,
            result.Id,
            result.ConflictSearch?.SearchNumber ?? 0,
            result.ConflictSearch?.SearchName ?? "Conflict search",
            result.MatchedName,
            result.RiskLevel,
            result.Score,
            result.EscalatedAt,
            result.EscalationNotes);
    }
}

public static class DashboardViews
{
    public const string Firm = "firm";
    public const string User = "user";
    public const string Partner = "partner";
}

internal sealed record CountBucket(string Label, int Count);
