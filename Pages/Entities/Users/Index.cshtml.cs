using MatterForge.Data;
using MatterForge.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Users;

public class IndexModel(MatterForgeDbContext db) : PageModel
{
    public List<MatterForgeUser> Users { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public bool Archived { get; set; }

    public async Task OnGetAsync()
    {
        Users = await db.Users
            .Include(x => x.ResponsibleMatters)
            .Where(x => x.IsArchived == Archived)
            .OrderBy(x => x.SystemId)
            .ToListAsync();
    }
}
