using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Users;

public class DetailsModel(MatterForgeDbContext db, DemoModeService demoModeService) : PageModel
{
    public MatterForgeUser? UserRecord { get; private set; }

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
            .Include(x => x.TeamMemberships)
                .ThenInclude(x => x.Team)
            .Include(x => x.Roles)
                .ThenInclude(x => x.SecurityRole)
            .FirstOrDefaultAsync(x => x.Id == id);
    }
}
