using System.ComponentModel.DataAnnotations;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Time;

public class CreateModel(
    CMIForgeDbContext db,
    CurrentUserService currentUserService,
    PermissionService permissionService) : PageModel
{
    [BindProperty]
    public TimeEntryInput Input { get; set; } = new();

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public List<TimeMatterOption> MatterOptions { get; private set; } = [];

    public List<TimePhaseOption> PhaseOptions { get; private set; } = [];

    public List<TimeTaskOption> TaskOptions { get; private set; } = [];

    public bool CanRecordForOthers { get; private set; }

    public string SystemIncrementLabel { get; private set; } = TimeIncrementRules.Label(TimeIncrementRules.SixMinutes);

    public async Task<IActionResult> OnGetAsync(Guid? matterId)
    {
        if (!await permissionService.HasAsync(PermissionKeys.TimeCreate))
        {
            return Forbid();
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        Input = new TimeEntryInput
        {
            UserId = currentUser?.Id ?? Guid.Empty,
            MatterId = matterId ?? Guid.Empty,
            WorkDate = DateOnly.FromDateTime(DateTime.Today),
            Hours = 0.5m,
            IsBillable = true
        };

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string actionType)
    {
        if (!await permissionService.HasAsync(PermissionKeys.TimeCreate))
        {
            return Forbid();
        }

        await LoadAsync();

        var currentUser = await currentUserService.GetCurrentUserAsync();
        if (!CanRecordForOthers && currentUser?.Id != Input.UserId)
        {
            ModelState.AddModelError("Input.UserId", "You can only record time for yourself.");
        }

        var matter = await db.Matters
            .Include(x => x.Client)
            .Include(x => x.TimeCodeSet)
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

        var nextNumber = (await db.TimeEntries.MaxAsync(x => (int?)x.TimeEntryNumber) ?? 0) + 1;
        var now = DateTimeOffset.UtcNow;
        var status = isSubmit && matter.RequiresTimeApproval
            ? TimeEntryStatuses.Submitted
            : isSubmit
                ? TimeEntryStatuses.Approved
                : TimeEntryStatuses.Draft;
        var entry = new TimeEntry
        {
            TimeEntryNumber = nextNumber,
            UserId = Input.UserId,
            ClientId = matter.Client.Id,
            MatterId = matter.Id,
            TimePhaseId = Input.TimePhaseId,
            TimeTaskId = Input.TimeTaskId,
            WorkDate = Input.WorkDate,
            Minutes = minutes,
            ClientNarrative = Input.ClientNarrative.Trim(),
            InternalNotes = Input.InternalNotes?.Trim() ?? string.Empty,
            IsBillable = Input.IsBillable,
            Status = status,
            SubmittedAt = isSubmit ? now : null,
            ApprovedAt = status == TimeEntryStatuses.Approved ? now : null,
            ApprovedByUserId = status == TimeEntryStatuses.Approved ? currentUser?.Id : null
        };

        db.TimeEntries.Add(entry);
        await db.SaveChangesAsync();
        return RedirectToPage("./Details", new { id = entry.Id });
    }

    private async Task LoadAsync()
    {
        CanRecordForOthers = await permissionService.HasAsync(PermissionKeys.TimeViewAll) ||
            await permissionService.HasAsync(PermissionKeys.TimeEdit);

        UserOptions = await db.Users
            .Where(x => x.IsActive && !x.IsArchived)
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem($"{x.DisplayName} ({x.SystemId:D8})", x.Id.ToString()))
            .ToListAsync();

        MatterOptions = await db.Matters
            .Include(x => x.Client)
            .Include(x => x.TimeCodeSet)
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

public sealed record TimeMatterOption(Guid Id, string Label, bool RequiresApproval, int? IncrementMinutes, Guid? TimeCodeSetId);

public sealed record TimePhaseOption(Guid Id, Guid TimeCodeSetId, string Code, string Name);

public sealed record TimeTaskOption(Guid Id, Guid TimeCodeSetId, Guid? TimePhaseId, string Code, string Name);

public class TimeEntryInput
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
}
