using MatterForge.Data;
using MatterForge.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Matters;

public class IndexModel(MatterForgeDbContext db) : PageModel
{
    public List<Matter> Matters { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Matters = await db.Matters
            .Include(x => x.Client)
            .Include(x => x.ResponsibleUser)
            .OrderBy(x => x.MatterNumber)
            .ToListAsync();
    }
}
