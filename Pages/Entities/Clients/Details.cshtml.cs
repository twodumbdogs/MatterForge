using MatterForge.Data;
using MatterForge.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Clients;

public class DetailsModel(MatterForgeDbContext db) : PageModel
{
    public Client? Client { get; private set; }

    public async Task OnGetAsync(Guid id)
    {
        Client = await db.Clients
            .Include(x => x.Matters)
                .ThenInclude(x => x.ResponsibleUser)
            .FirstOrDefaultAsync(x => x.Id == id);
    }
}
