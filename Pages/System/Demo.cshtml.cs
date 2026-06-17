using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatterForge.Pages.System;

public class DemoModel(
    DemoModeService demoModeService,
    DemoResetService demoResetService) : PageModel
{
    [TempData]
    public string? ResetMessage { get; set; }

    public bool IsDemoMode => demoModeService.IsEnabled;

    public double ResetIntervalHours => demoModeService.ResetIntervalHours;

    public IActionResult OnGet()
    {
        return IsDemoMode ? Page() : NotFound();
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
}
