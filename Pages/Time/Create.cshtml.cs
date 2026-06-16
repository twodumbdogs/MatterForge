using System.ComponentModel.DataAnnotations;
using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Time;

public class CreateModel(
    MatterForgeDbContext db,
    CurrentUserService currentUserService,
    PermissionService permissionService) : PageModel
{
    [BindProperty]
    public TimeEntryInput Input { get; set; } = new();

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public List<SelectListItem> MatterOptions { get; private set; } = [];

    public List<SelectListItem> StatusOptions { get; private set; } = [];

    public bool CanRecordForOthers { get; private set; }

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
            IsBillable = true,
            Status = TimeEntryStatuses.Draft
        };

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
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
            .FirstOrDefaultAsync(x => x.Id == Input.MatterId);
        if (matter?.Client is null)
        {
            ModelState.AddModelError("Input.MatterId", "Choose a valid matter.");
        }

        if (!TimeEntryStatuses.All.Contains(Input.Status))
        {
            ModelState.AddModelError("Input.Status", "Choose a valid status.");
        }

        var minutes = (int)Math.Round(Input.Hours * 60m, MidpointRounding.AwayFromZero);
        if (minutes <= 0)
        {
            ModelState.AddModelError("Input.Hours", "Enter more than zero time.");
        }

        if (!ModelState.IsValid || matter?.Client is null)
        {
            return Page();
        }

        var nextNumber = (await db.TimeEntries.MaxAsync(x => (int?)x.TimeEntryNumber) ?? 0) + 1;
        var entry = new TimeEntry
        {
            TimeEntryNumber = nextNumber,
            UserId = Input.UserId,
            ClientId = matter.Client.Id,
            MatterId = matter.Id,
            WorkDate = Input.WorkDate,
            Minutes = minutes,
            Narrative = Input.Narrative.Trim(),
            IsBillable = Input.IsBillable,
            Status = Input.Status
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
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem($"{x.DisplayName} ({x.SystemId:D8})", x.Id.ToString()))
            .ToListAsync();

        MatterOptions = await db.Matters
            .Include(x => x.Client)
            .OrderBy(x => x.MatterNumber)
            .Select(x => new SelectListItem($"{x.MatterNumber:D8} - {x.Client!.Name} / {x.Name}", x.Id.ToString()))
            .ToListAsync();

        StatusOptions = TimeEntryStatuses.All
            .Select(x => new SelectListItem(x, x))
            .ToList();
    }
}

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

    [Required]
    [StringLength(2000)]
    public string Narrative { get; set; } = string.Empty;

    [Display(Name = "Billable")]
    public bool IsBillable { get; set; } = true;

    [Required]
    public string Status { get; set; } = TimeEntryStatuses.Draft;
}
