using MatterForge.Data;
using MatterForge.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Clients;

public class IndexModel(MatterForgeDbContext db) : PageModel
{
    public List<Client> Clients { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Clients = await db.Clients
            .Include(x => x.Matters)
            .OrderBy(x => x.ClientNumber)
            .ToListAsync();
    }
}
