using MatterForge.Data;
using MatterForge.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Users;

public class IndexModel(MatterForgeDbContext db) : PageModel
{
    public List<MatterForgeUser> Users { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Users = await db.Users
            .Include(x => x.ResponsibleMatters)
            .OrderBy(x => x.SystemId)
            .ToListAsync();
    }
}
