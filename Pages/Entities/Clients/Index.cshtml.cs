using MatterForge.Data;
using MatterForge.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Clients;

public class IndexModel(MatterForgeDbContext db) : PageModel
{
    public List<Client> Clients { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public bool Archived { get; set; }

    public async Task OnGetAsync()
    {
        Clients = await db.Clients
            .Include(x => x.Matters)
            .Where(x => x.IsArchived == Archived)
            .OrderBy(x => x.ClientNumber)
            .ToListAsync();
    }
}
