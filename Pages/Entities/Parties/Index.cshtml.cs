using MatterForge.Data;
using MatterForge.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Parties;

public class IndexModel(MatterForgeDbContext db) : PageModel
{
    public List<Party> Parties { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public bool Archived { get; set; }

    public async Task OnGetAsync()
    {
        Parties = await db.Parties
            .Include(x => x.Aliases)
            .Include(x => x.MatterParties)
            .Where(x => x.IsArchived == Archived)
            .OrderBy(x => x.PartyNumber)
            .ToListAsync();
    }
}
