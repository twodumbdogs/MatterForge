using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Matters;

public class DetailsModel(
    MatterForgeDbContext db,
    PermissionService permissionService,
    CurrentUserService currentUserService) : PageModel
{
    public Matter? Matter { get; private set; }

    public bool CanRunConflicts { get; private set; }

    public bool CanRecordTime { get; private set; }

    public bool CanViewTime { get; private set; }

    public bool CanViewAllTime { get; private set; }

    public decimal TotalTimeHours { get; private set; }

    public async Task OnGetAsync(Guid id)
    {
        CanRunConflicts = await permissionService.HasAsync(PermissionKeys.ConflictsRun);
        CanRecordTime = await permissionService.HasAsync(PermissionKeys.TimeCreate);
        CanViewAllTime = await permissionService.HasAsync(PermissionKeys.TimeViewAll);
        CanViewTime = CanViewAllTime || await permissionService.HasAsync(PermissionKeys.TimeViewOwn);
        Matter = await db.Matters
            .Include(x => x.Client)
            .Include(x => x.ResponsibleUser)
            .Include(x => x.Parties)
                .ThenInclude(x => x.Party)
            .Include(x => x.TimeEntries)
                .ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Matter is not null)
        {
            if (CanViewTime && !CanViewAllTime)
            {
                var currentUser = await currentUserService.GetCurrentUserAsync();
                Matter.TimeEntries = currentUser is null
                    ? []
                    : Matter.TimeEntries.Where(x => x.UserId == currentUser.Id).ToList();
            }

            TotalTimeHours = Matter.TimeEntries.Sum(x => x.Minutes) / 60m;
        }
    }
}
