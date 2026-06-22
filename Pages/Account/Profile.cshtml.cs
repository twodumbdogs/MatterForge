using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace CMIForge.Pages.Account;

public class ProfileModel(
    CMIForgeDbContext db,
    CurrentUserService currentUserService) : PageModel
{
    private static readonly HashSet<int> AllowedFontScalePercents = [90, 100, 115, 130];

    [BindProperty]
    public ProfileInput Input { get; set; } = new();

    public CMIForgeUser? CurrentUser { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        CurrentUser = await currentUserService.GetCurrentUserAsync();
        if (CurrentUser is null)
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        Input.FontScalePercent = NormalizeFontScale(CurrentUser.FontScalePercent);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        CurrentUser = await currentUserService.GetCurrentUserAsync();
        if (CurrentUser is null)
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        if (!AllowedFontScalePercents.Contains(Input.FontScalePercent))
        {
            ModelState.AddModelError("Input.FontScalePercent", "Choose one of the available font sizes.");
        }

        if (!ModelState.IsValid)
        {
            Input.FontScalePercent = NormalizeFontScale(Input.FontScalePercent);
            return Page();
        }

        CurrentUser.FontScalePercent = Input.FontScalePercent;
        CurrentUser.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        TempData["StatusMessage"] = "Profile settings saved.";
        return RedirectToPage();
    }

    private static int NormalizeFontScale(int value)
    {
        return AllowedFontScalePercents.Contains(value) ? value : 100;
    }
}

public class ProfileInput
{
    [Display(Name = "Font size")]
    public int FontScalePercent { get; set; } = 100;
}
