using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.System;

public class StatsModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    SystemTelemetryService telemetry) : PageModel
{
    public SystemTelemetrySnapshot Telemetry { get; private set; } = telemetry.GetSnapshot();

    public List<MetricCard> MetricCards { get; private set; } = [];

    public List<RecordCountMetric> RecordCounts { get; private set; } = [];

    public List<OperationalMetric> OperationalMetrics { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        Telemetry = telemetry.GetSnapshot();
        MetricCards = BuildMetricCards(Telemetry);
        RecordCounts = await LoadRecordCountsAsync();
        OperationalMetrics = await LoadOperationalMetricsAsync();

        return Page();
    }

    private static List<MetricCard> BuildMetricCards(SystemTelemetrySnapshot snapshot)
    {
        return
        [
            new("Uptime", FormatDuration(snapshot.Uptime), $"Started {snapshot.StartedAt.LocalDateTime:g}"),
            new("Requests", snapshot.TotalRequests.ToString("N0"), $"{snapshot.AverageRequestMilliseconds:N0} ms avg"),
            new("Slowest page", snapshot.SlowestRequestMilliseconds > 0 ? $"{snapshot.SlowestRequestMilliseconds:N0} ms" : "No data yet", string.IsNullOrWhiteSpace(snapshot.SlowestRequestPath) ? "No page requests yet" : snapshot.SlowestRequestPath),
            new("Conflict searches", snapshot.TotalConflictSearches.ToString("N0"), snapshot.TotalConflictSearches == 0 ? "No searches since restart" : $"{snapshot.AverageConflictSearchMilliseconds:N0} ms avg"),
            new("Last search", snapshot.LastConflictSearchAt.HasValue ? $"{snapshot.LastConflictSearchMilliseconds:N0} ms" : "No data yet", snapshot.LastConflictSearchAt.HasValue ? $"{snapshot.LastConflictSearchTermCount:N0} terms, {snapshot.LastConflictSearchResultCount:N0} hits" : "Run a search to populate this"),
            new("Slowest search", snapshot.SlowestConflictSearchMilliseconds > 0 ? $"{snapshot.SlowestConflictSearchMilliseconds:N0} ms" : "No data yet", "Current app-process lifetime")
        ];
    }

    private async Task<List<RecordCountMetric>> LoadRecordCountsAsync()
    {
        return
        [
            new("Users", await db.Users.AsNoTracking().CountAsync(x => !x.IsArchived)),
            new("Clients", await db.Clients.AsNoTracking().CountAsync(x => !x.IsArchived)),
            new("Matters", await db.Matters.AsNoTracking().CountAsync(x => !x.IsArchived)),
            new("Parties", await db.Parties.AsNoTracking().CountAsync(x => !x.IsArchived)),
            new("Contacts", await db.Contacts.AsNoTracking().CountAsync(x => !x.IsArchived)),
            new("Submissions", await db.FormSubmissions.AsNoTracking().CountAsync()),
            new("Conflict searches", await db.ConflictSearches.AsNoTracking().CountAsync(x => x.ArchivedAt == null)),
            new("Conflict hits", await db.ConflictSearchResults.AsNoTracking().CountAsync()),
            new("Conflict search docs", await db.ConflictSearchDocuments.AsNoTracking().CountAsync()),
            new("Time entries", await db.TimeEntries.AsNoTracking().CountAsync()),
            new("Import batches", await db.ImportBatches.AsNoTracking().CountAsync()),
            new("Audit events", await db.AuditLogs.AsNoTracking().CountAsync())
        ];
    }

    private async Task<List<OperationalMetric>> LoadOperationalMetricsAsync()
    {
        var twentyFourHoursAgo = DateTimeOffset.UtcNow.AddDays(-1);
        return
        [
            new("Open workflow tasks", await db.SubmissionWorkflowTasks.AsNoTracking().CountAsync(x => x.Status == "Open"), "Items waiting on a user or team."),
            new("Pending conflict reviews", await db.ConflictSearches.AsNoTracking().CountAsync(x => x.ArchivedAt == null && x.ReviewerDecision == ConflictSearchDecisions.Pending), "Searches without a final human decision."),
            new("Imports with errors", await db.ImportBatches.AsNoTracking().CountAsync(x => x.Status == ImportBatchStatuses.Failed || x.Status == ImportBatchStatuses.CompletedWithErrors || x.Status == ImportBatchStatuses.ValidationIssues), "Import batches that need attention."),
            new("Submissions last 24h", await db.FormSubmissions.AsNoTracking().CountAsync(x => x.SubmittedAt >= twentyFourHoursAgo), "Recent intake volume."),
            new("Searches last 24h", await db.ConflictSearches.AsNoTracking().CountAsync(x => x.CreatedAt >= twentyFourHoursAgo), "Recent conflict-search volume."),
            new("Time entries last 24h", await db.TimeEntries.AsNoTracking().CountAsync(x => x.CreatedAt >= twentyFourHoursAgo), "Recent timekeeping activity.")
        ];
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            return $"{(int)duration.TotalDays:N0}d {duration.Hours}h";
        }

        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours:N0}h {duration.Minutes}m";
        }

        return $"{Math.Max(0, (int)duration.TotalMinutes):N0}m {duration.Seconds}s";
    }
}

public sealed record MetricCard(string Label, string Value, string Detail);

public sealed record RecordCountMetric(string Label, int Count);

public sealed record OperationalMetric(string Label, int Count, string Description);
