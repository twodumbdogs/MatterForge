using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Security;

public class AuditModel(CMIForgeDbContext db, PermissionService permissionService) : PageModel
{
    public List<AuditLog> AuditLogs { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        AuditLogs = await db.AuditLogs
            .Include(x => x.ActorUser)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync();

        return Page();
    }
}
