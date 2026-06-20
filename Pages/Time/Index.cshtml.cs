using System.Text;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Time;

public class IndexModel(
    CMIForgeDbContext db,
    CurrentUserService currentUserService,
    PermissionService permissionService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? MatterId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public List<TimeEntry> Entries { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public List<SelectListItem> MatterOptions { get; private set; } = [];

    public List<SelectListItem> StatusOptions { get; private set; } = [];

    public bool CanViewAll { get; private set; }

    public bool CanCreate { get; private set; }

    public decimal TotalHours { get; private set; }

    public decimal BillableHours { get; private set; }

    public decimal NonBillableHours { get; private set; }

    public int DraftCount { get; private set; }

    public int ExportedCount { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await CanViewTimeAsync())
        {
            return Forbid();
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        if (!await CanViewTimeAsync())
        {
            return Forbid();
        }

        var entries = await BuildQueryAsync();
        var currentUser = await currentUserService.GetCurrentUserAsync();
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in entries.Where(x => x.Status == TimeEntryStatuses.Approved && !x.ExportedAt.HasValue))
        {
            entry.ExportedAt = now;
            entry.ExportedByUserId = currentUser?.Id;
            entry.UpdatedAt = now;
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync();
        }

        var csv = new StringBuilder();
        csv.AppendLine("TimeEntryNumber,WorkDate,User,Client,Matter,Phase,Task,Hours,Billable,Status,Exported,ClientNarrative,InternalNotes");
        foreach (var entry in entries)
        {
            csv.AppendLine(string.Join(",",
                entry.TimeEntryNumber.ToString("D8"),
                entry.WorkDate.ToString("yyyy-MM-dd"),
                Csv(entry.User?.DisplayName),
                Csv(entry.Client?.Name),
                Csv(entry.Matter?.Name),
                Csv(entry.TimePhase is null ? string.Empty : $"{entry.TimePhase.Code} - {entry.TimePhase.Name}"),
                Csv(entry.TimeTask is null ? string.Empty : $"{entry.TimeTask.Code} - {entry.TimeTask.Name}"),
                (entry.Minutes / 60m).ToString("0.00"),
                entry.IsBillable ? "Yes" : "No",
                Csv(entry.Status),
                entry.ExportedAt.HasValue ? "Yes" : "No",
                Csv(entry.ClientNarrative),
                Csv(entry.InternalNotes)));
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "cmiforge-time-entries.csv");
    }

    private async Task<bool> CanViewTimeAsync()
    {
        CanViewAll = await permissionService.HasAsync(PermissionKeys.TimeViewAll);
        CanCreate = await permissionService.HasAsync(PermissionKeys.TimeCreate);
        return CanViewAll || await permissionService.HasAsync(PermissionKeys.TimeViewOwn);
    }

    private async Task LoadAsync()
    {
        Entries = await BuildQueryAsync();
        TotalHours = Entries.Sum(x => x.Minutes) / 60m;
        BillableHours = Entries.Where(x => x.IsBillable).Sum(x => x.Minutes) / 60m;
        NonBillableHours = TotalHours - BillableHours;
        DraftCount = Entries.Count(x => x.Status == TimeEntryStatuses.Draft);
        ExportedCount = Entries.Count(x => x.ExportedAt.HasValue);

        UserOptions = await db.Users
            .Where(x => x.IsActive && !x.IsArchived)
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem($"{x.DisplayName} ({x.SystemId:D8})", x.Id.ToString()))
            .ToListAsync();

        MatterOptions = await db.Matters
            .Include(x => x.Client)
            .Where(x => !x.IsArchived)
            .OrderBy(x => x.MatterNumber)
            .Select(x => new SelectListItem($"{x.MatterNumber:D8} - {x.Client!.Name} / {x.Name}", x.Id.ToString()))
            .ToListAsync();

        StatusOptions = TimeEntryStatuses.All
            .Select(x => new SelectListItem(x, x))
            .ToList();
    }

    private async Task<List<TimeEntry>> BuildQueryAsync()
    {
        var query = db.TimeEntries
            .Include(x => x.User)
            .Include(x => x.Client)
            .Include(x => x.Matter)
            .Include(x => x.TimePhase)
            .Include(x => x.TimeTask)
            .AsQueryable();

        if (!CanViewAll)
        {
            var currentUser = await currentUserService.GetCurrentUserAsync();
            if (currentUser is null)
            {
                return [];
            }

            query = query.Where(x => x.UserId == currentUser.Id);
        }
        else if (UserId.HasValue)
        {
            query = query.Where(x => x.UserId == UserId.Value);
        }

        if (From.HasValue)
        {
            query = query.Where(x => x.WorkDate >= From.Value);
        }

        if (To.HasValue)
        {
            query = query.Where(x => x.WorkDate <= To.Value);
        }

        if (MatterId.HasValue)
        {
            query = query.Where(x => x.MatterId == MatterId.Value);
        }

        if (!string.IsNullOrWhiteSpace(Status))
        {
            query = query.Where(x => x.Status == Status);
        }

        return await query
            .OrderByDescending(x => x.WorkDate)
            .ThenByDescending(x => x.CreatedAt)
            .Take(250)
            .ToListAsync();
    }

    private static string Csv(string? value)
    {
        var safeValue = value ?? string.Empty;
        return "\"" + safeValue.Replace("\"", "\"\"") + "\"";
    }
}
