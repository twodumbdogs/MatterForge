using System.ComponentModel.DataAnnotations;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Time;

public class EditModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    CurrentUserService currentUserService) : PageModel
{
    [BindProperty]
    public TimeEntryEditInput Input { get; set; } = new();

    public TimeEntry? Entry { get; private set; }

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public List<TimeMatterOption> MatterOptions { get; private set; } = [];

    public List<TimePhaseOption> PhaseOptions { get; private set; } = [];

    public List<TimeTaskOption> TaskOptions { get; private set; } = [];

    public bool CanRecordForOthers { get; private set; }

    public string SystemIncrementLabel { get; private set; } = TimeIncrementRules.Label(TimeIncrementRules.SixMinutes);

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.TimeEdit))
        {
            return Forbid();
        }

        await LoadAsync(id);
        if (Entry is null)
        {
            return Page();
        }

        if (Entry.IsLocked)
        {
            return RedirectToPage("./Details", new { id });
        }

        Input = TimeEntryEditInput.FromEntry(Entry);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, string actionType)
    {
        if (!await permissionService.HasAsync(PermissionKeys.TimeEdit))
        {
            return Forbid();
        }

        await LoadAsync(id);
        if (Entry is null)
        {
            return Page();
        }

        if (Entry.IsLocked)
        {
            return RedirectToPage("./Details", new { id });
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        if (!CanRecordForOthers && currentUser?.Id != Input.UserId)
        {
            ModelState.AddModelError("Input.UserId", "You can only record time for yourself.");
        }

        var matter = await db.Matters
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == Input.MatterId);
        if (matter?.Client is null)
        {
            ModelState.AddModelError("Input.MatterId", "Choose a valid matter.");
        }

        var isSubmit = actionType.Equals("submit", StringComparison.OrdinalIgnoreCase);
        if (!isSubmit && !actionType.Equals("draft", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Choose whether to save as draft or submit the time.");
        }

        if (Input.TimePhaseId.HasValue && matter?.TimeCodeSetId is not null)
        {
            var phaseIsValid = await db.TimePhases.AnyAsync(x =>
                x.Id == Input.TimePhaseId.Value &&
                x.TimeCodeSetId == matter.TimeCodeSetId.Value &&
                x.IsActive);
            if (!phaseIsValid)
            {
                ModelState.AddModelError("Input.TimePhaseId", "Choose a valid phase for this matter.");
            }
        }

        if (Input.TimeTaskId.HasValue && matter?.TimeCodeSetId is not null)
        {
            var taskIsValid = await db.TimeTasks.AnyAsync(x =>
                x.Id == Input.TimeTaskId.Value &&
                x.TimeCodeSetId == matter.TimeCodeSetId.Value &&
                (!Input.TimePhaseId.HasValue || x.TimePhaseId == Input.TimePhaseId.Value) &&
                x.IsActive);
            if (!taskIsValid)
            {
                ModelState.AddModelError("Input.TimeTaskId", "Choose a valid task for this matter and phase.");
            }
        }

        if ((Input.TimePhaseId.HasValue || Input.TimeTaskId.HasValue) && matter?.TimeCodeSetId is null)
        {
            ModelState.AddModelError("Input.TimePhaseId", "This matter does not have a time code set.");
        }

        var rawMinutes = (int)Math.Round(Input.Hours * 60m, MidpointRounding.AwayFromZero);
        var increment = matter is null ? TimeIncrementRules.SixMinutes : await ResolveIncrementAsync(matter);
        var minutes = TimeIncrementRules.RoundMinutes(rawMinutes, increment);
        if (minutes <= 0)
        {
            ModelState.AddModelError("Input.Hours", "Enter more than zero time.");
        }

        if (!ModelState.IsValid || matter?.Client is null)
        {
            return Page();
        }

        var now = DateTimeOffset.UtcNow;
        Entry.UserId = Input.UserId;
        Entry.ClientId = matter.Client.Id;
        Entry.MatterId = matter.Id;
        Entry.TimePhaseId = Input.TimePhaseId;
        Entry.TimeTaskId = Input.TimeTaskId;
        Entry.WorkDate = Input.WorkDate;
        Entry.Minutes = minutes;
        Entry.ClientNarrative = Input.ClientNarrative.Trim();
        Entry.InternalNotes = Input.InternalNotes?.Trim() ?? string.Empty;
        Entry.IsBillable = Input.IsBillable;
        Entry.Status = isSubmit && matter.RequiresTimeApproval
            ? TimeEntryStatuses.Submitted
            : isSubmit
                ? TimeEntryStatuses.Approved
                : TimeEntryStatuses.Draft;
        Entry.SubmittedAt = isSubmit ? now : null;
        Entry.ApprovedAt = Entry.Status == TimeEntryStatuses.Approved ? now : null;
        Entry.ApprovedByUserId = Entry.Status == TimeEntryStatuses.Approved ? currentUser?.Id : null;
        Entry.UpdatedAt = now;

        await db.SaveChangesAsync();
        return RedirectToPage("./Details", new { id = Entry.Id });
    }

    private async Task LoadAsync(Guid id)
    {
        CanRecordForOthers = await permissionService.HasAsync(PermissionKeys.TimeViewAll);
        Entry = await db.TimeEntries
            .Include(x => x.Matter)
            .FirstOrDefaultAsync(x => x.Id == id);

        UserOptions = await db.Users
            .Where(x => x.IsActive && !x.IsArchived)
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem($"{x.DisplayName} ({x.SystemId:D8})", x.Id.ToString()))
            .ToListAsync();

        MatterOptions = await db.Matters
            .Include(x => x.Client)
            .Where(x => !x.IsArchived)
            .OrderBy(x => x.MatterNumber)
            .Select(x => new TimeMatterOption(
                x.Id,
                $"{x.MatterNumber:D8} - {x.Client!.Name} / {x.Name}",
                x.RequiresTimeApproval,
                x.TimeIncrementMinutes,
                x.TimeCodeSetId))
            .ToListAsync();

        PhaseOptions = await db.TimePhases
            .Where(x => x.IsActive)
            .OrderBy(x => x.TimeCodeSet!.Name)
            .ThenBy(x => x.SortOrder)
            .Select(x => new TimePhaseOption(x.Id, x.TimeCodeSetId, x.Code, x.Name))
            .ToListAsync();

        TaskOptions = await db.TimeTasks
            .Where(x => x.IsActive)
            .OrderBy(x => x.TimeCodeSet!.Name)
            .ThenBy(x => x.SortOrder)
            .Select(x => new TimeTaskOption(x.Id, x.TimeCodeSetId, x.TimePhaseId, x.Code, x.Name))
            .ToListAsync();

        SystemIncrementLabel = TimeIncrementRules.Label(await GetSystemIncrementAsync());
    }

    private async Task<int> ResolveIncrementAsync(Matter matter)
    {
        return TimeIncrementRules.Resolve(matter.TimeIncrementMinutes, await GetSystemIncrementAsync());
    }

    private async Task<int> GetSystemIncrementAsync()
    {
        var value = await db.SystemSettings
            .Where(x => x.Key == TimeIncrementRules.SystemDefaultSettingKey)
            .Select(x => x.Value)
            .FirstOrDefaultAsync();
        return int.TryParse(value, out var parsed)
            ? TimeIncrementRules.Normalize(parsed)
            : TimeIncrementRules.SixMinutes;
    }
}

public class TimeEntryEditInput
{
    [Required]
    [Display(Name = "User")]
    public Guid UserId { get; set; }

    [Required]
    [Display(Name = "Matter")]
    public Guid MatterId { get; set; }

    [Required]
    [Display(Name = "Work date")]
    public DateOnly WorkDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(0.01, 24)]
    public decimal Hours { get; set; } = 0.5m;

    [Display(Name = "Phase")]
    public Guid? TimePhaseId { get; set; }

    [Display(Name = "Task")]
    public Guid? TimeTaskId { get; set; }

    [Required]
    [StringLength(2000)]
    [Display(Name = "Client narrative")]
    public string ClientNarrative { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Internal notes")]
    public string? InternalNotes { get; set; }

    [Display(Name = "Billable")]
    public bool IsBillable { get; set; } = true;

    public static TimeEntryEditInput FromEntry(TimeEntry entry)
    {
        return new TimeEntryEditInput
        {
            UserId = entry.UserId,
            MatterId = entry.MatterId,
            WorkDate = entry.WorkDate,
            Hours = entry.Minutes / 60m,
            TimePhaseId = entry.TimePhaseId,
            TimeTaskId = entry.TimeTaskId,
            ClientNarrative = entry.ClientNarrative,
            InternalNotes = entry.InternalNotes,
            IsBillable = entry.IsBillable
        };
    }
}
