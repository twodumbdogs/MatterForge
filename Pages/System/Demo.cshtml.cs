using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.System;

public class DemoModel(
    DemoModeService demoModeService,
    DemoResetService demoResetService,
    MatterForgeDbContext db) : PageModel
{
    public List<DemoResetRun> RecentRuns { get; private set; } = [];

    public DemoResetRun? LastCompletedRun => RecentRuns
        .Where(x => x.CompletedAt.HasValue)
        .OrderByDescending(x => x.CompletedAt)
        .FirstOrDefault();

    public DateTimeOffset? NextEstimatedResetAt =>
        LastCompletedRun?.CompletedAt?.AddHours(ResetIntervalHours);

    [TempData]
    public string? ResetMessage { get; set; }

    public bool IsDemoMode => demoModeService.IsEnabled;

    public double ResetIntervalHours => demoModeService.ResetIntervalHours;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsDemoMode)
        {
            return NotFound();
        }

        await LoadRunsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostResetAsync()
    {
        if (!IsDemoMode)
        {
            return NotFound();
        }

        var result = await demoResetService.ResetAsync("Manual reset", HttpContext.RequestAborted);
        ResetMessage = result.Succeeded
            ? $"{result.Message} Removed {result.DeletedRows:N0} demo row(s) before reseeding."
            : result.Message;

        return RedirectToPage();
    }

    private async Task LoadRunsAsync()
    {
        RecentRuns = await db.DemoResetRuns
            .OrderByDescending(x => x.StartedAt)
            .Take(20)
            .ToListAsync();
    }
}
