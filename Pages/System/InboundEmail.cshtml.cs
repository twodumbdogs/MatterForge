using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.System;

public class InboundEmailModel(
    CMIForgeDbContext db,
    PermissionService permissionService) : PageModel
{
    public List<InboundEmailMessage> Messages { get; private set; } = [];

    public bool CanView { get; private set; }

    public async Task OnGetAsync()
    {
        CanView = await permissionService.HasAsync(PermissionKeys.SecurityManage);
        if (!CanView)
        {
            return;
        }

        Messages = await db.InboundEmailMessages
            .AsNoTracking()
            .Include(x => x.FormSubmission)
            .Include(x => x.Attachments)
            .OrderByDescending(x => x.ReceivedAt)
            .Take(100)
            .ToListAsync();
    }
}
