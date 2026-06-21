using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Security;

public class ImpersonationModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    CurrentUserService currentUserService,
    AuditLogService auditLogService) : PageModel
{
    public CMIForgeUser? ActualUser { get; private set; }

    public UserImpersonationContext? ActiveImpersonation { get; private set; }

    public List<CMIForgeUser> UserOptions { get; private set; } = [];

    [BindProperty]
    public Guid TargetUserId { get; set; }

    [BindProperty]
    public string Notes { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsActualUserAsync(PermissionKeys.SystemImpersonateUsers))
        {
            return Forbid();
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostStartAsync()
    {
        if (!await permissionService.HasAsActualUserAsync(PermissionKeys.SystemImpersonateUsers))
        {
            return Forbid();
        }

        ActualUser = await currentUserService.GetActualCurrentUserAsync();
        if (ActualUser is null)
        {
            return Forbid();
        }

        var targetUser = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == TargetUserId && x.IsActive && !x.IsArchived);
        if (targetUser is null)
        {
            ModelState.AddModelError(nameof(TargetUserId), "Choose an active user to impersonate.");
            await LoadAsync();
            return Page();
        }

        if (targetUser.Id == ActualUser.Id)
        {
            ModelState.AddModelError(nameof(TargetUserId), "Choose a different user, or stop impersonating to return to yourself.");
            await LoadAsync();
            return Page();
        }

        var started = await currentUserService.StartImpersonationAsync(targetUser.Id);
        if (started is null)
        {
            ModelState.AddModelError(string.Empty, "Impersonation could not be started.");
            await LoadAsync();
            return Page();
        }

        await auditLogService.LogAsync(
            "Security.ImpersonationStarted",
            "User",
            targetUser.Id,
            targetUser.SystemId.ToString("D8"),
            $"{ActualUser.DisplayName} started viewing CMIForge as {targetUser.DisplayName}.",
            new
            {
                ActualUserId = ActualUser.Id,
                ActualUserName = ActualUser.DisplayName,
                TargetUserId = targetUser.Id,
                TargetUserName = targetUser.DisplayName,
                Notes
            });

        TempData["StatusMessage"] = $"Now viewing as {targetUser.DisplayName}.";
        return RedirectToPage("/Index", new { view = "user" });
    }

    public async Task<IActionResult> OnPostStopAsync()
    {
        var activeImpersonation = await currentUserService.GetImpersonationContextAsync();
        if (activeImpersonation is not null)
        {
            await auditLogService.LogAsync(
                "Security.ImpersonationStopped",
                "User",
                activeImpersonation.ImpersonatedUser.Id,
                activeImpersonation.ImpersonatedUser.SystemId.ToString("D8"),
                $"{activeImpersonation.ActualUser.DisplayName} stopped viewing CMIForge as {activeImpersonation.ImpersonatedUser.DisplayName}.",
                new
                {
                    ActualUserId = activeImpersonation.ActualUser.Id,
                    ActualUserName = activeImpersonation.ActualUser.DisplayName,
                    TargetUserId = activeImpersonation.ImpersonatedUser.Id,
                    TargetUserName = activeImpersonation.ImpersonatedUser.DisplayName
                });
        }

        currentUserService.StopImpersonation();
        TempData["StatusMessage"] = "Impersonation stopped.";

        var returnUrl = Request.Headers.Referer.ToString();
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToPage("./Impersonation");
    }

    private async Task LoadAsync()
    {
        ActualUser = await currentUserService.GetActualCurrentUserAsync();
        ActiveImpersonation = await currentUserService.GetImpersonationContextAsync();
        UserOptions = await db.Users
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsArchived)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.SystemId)
            .ToListAsync();
    }
}
