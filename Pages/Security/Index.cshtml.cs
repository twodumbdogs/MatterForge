using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Security;

public class IndexModel(CMIForgeDbContext db, PermissionService permissionService) : PageModel
{
    public CMIForgeUser? CurrentUser { get; private set; }

    public List<Team> Teams { get; private set; } = [];

    public List<SecurityRole> Roles { get; private set; } = [];

    public List<Permission> Permissions { get; private set; } = [];

    public int AuditLogCount { get; private set; }

    public bool CanImpersonateUsers { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        CurrentUser = await permissionService.GetCurrentUserAsync();
        CanImpersonateUsers = await permissionService.HasAsActualUserAsync(PermissionKeys.SystemImpersonateUsers);

        Teams = await db.Teams
            .Include(x => x.Members)
            .Include(x => x.Roles)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        Roles = await db.SecurityRoles
            .Include(x => x.RolePermissions)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        Permissions = await db.Permissions
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .ToListAsync();

        AuditLogCount = await db.AuditLogs.CountAsync();

        return Page();
    }
}
