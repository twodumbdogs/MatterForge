using MatterForge.Data;
using MatterForge.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Parties;

public class IndexModel(MatterForgeDbContext db) : PageModel
{
    public List<Party> Parties { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Parties = await db.Parties
            .Include(x => x.Aliases)
            .Include(x => x.MatterParties)
            .OrderBy(x => x.PartyNumber)
            .ToListAsync();
    }
}
