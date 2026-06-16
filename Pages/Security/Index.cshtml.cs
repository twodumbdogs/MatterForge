using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Security;

public class IndexModel(MatterForgeDbContext db, PermissionService permissionService) : PageModel
{
    public MatterForgeUser? CurrentUser { get; private set; }

    public List<Team> Teams { get; private set; } = [];

    public List<SecurityRole> Roles { get; private set; } = [];

    public List<Permission> Permissions { get; private set; } = [];

    public int AuditLogCount { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        CurrentUser = await permissionService.GetCurrentUserAsync();

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
