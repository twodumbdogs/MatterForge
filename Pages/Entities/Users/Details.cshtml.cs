using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Users;

public class DetailsModel(
    CMIForgeDbContext db,
    DemoModeService demoModeService,
    PermissionService permissionService,
    AuditLogService auditLogService) : PageModel
{
    public CMIForgeUser? UserRecord { get; private set; }

    public List<AuditLog> AuditHistory { get; private set; } = [];

    public bool IsEditLocked => UserRecord is not null && demoModeService.IsProtectedSystemUser(UserRecord.SystemId);

    public string ProtectedUserMessage => demoModeService.ProtectedUserMessage;

    [TempData]
    public string? CreatedEntraUserPrincipalName { get; set; }

    [TempData]
    public string? CreatedEntraTemporaryPassword { get; set; }

    public async Task OnGetAsync(Guid id)
    {
        UserRecord = await db.Users
            .Include(x => x.ResponsibleMatters)
                .ThenInclude(x => x.Client)
            .Include(x => x.LeadPartnerMatters)
                .ThenInclude(x => x.Client)
            .Include(x => x.TeamMemberships)
                .ThenInclude(x => x.Team)
            .Include(x => x.Roles)
                .ThenInclude(x => x.SecurityRole)
            .FirstOrDefaultAsync(x => x.Id == id);

        AuditHistory = UserRecord is null
            ? []
            : await auditLogService.ListForEntityAsync("User", id);
    }

    public async Task<IActionResult> OnPostArchiveAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        if (demoModeService.IsProtectedSystemUser(user.SystemId))
        {
            return RedirectToPage(new { id });
        }

        user.IsArchived = !user.IsArchived;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            user.IsArchived ? "User.Archived" : "User.Restored",
            "User",
            user.Id,
            user.SystemId.ToString("D8"),
            $"{(user.IsArchived ? "Archived" : "Restored")} user {user.DisplayName}.");

        return RedirectToPage(new { id });
    }
}