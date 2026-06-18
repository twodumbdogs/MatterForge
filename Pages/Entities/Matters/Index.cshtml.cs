using MatterForge.Data;
using MatterForge.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Matters;

public class IndexModel(MatterForgeDbContext db) : PageModel
{
    public List<Matter> Matters { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public bool Archived { get; set; }

    public async Task OnGetAsync()
    {
        Matters = await db.Matters
            .Include(x => x.Client)
            .Include(x => x.ResponsibleUser)
            .Where(x => x.IsArchived == Archived)
            .OrderBy(x => x.MatterNumber)
            .ToListAsync();
    }
}
