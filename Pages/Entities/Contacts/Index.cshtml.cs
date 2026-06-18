using MatterForge.Data;
using MatterForge.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Contacts;

public class IndexModel(MatterForgeDbContext db) : PageModel
{
    public List<Contact> Contacts { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Contacts = await db.Contacts
            .Include(x => x.ClientLinks)
            .Include(x => x.MatterLinks)
            .OrderBy(x => x.ContactNumber)
            .ToListAsync();
    }
}
