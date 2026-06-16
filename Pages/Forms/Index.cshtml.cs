using MatterForge.Data;
using MatterForge.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Forms;

public class IndexModel(MatterForgeDbContext db) : PageModel
{
    public List<FormDefinition> Forms { get; private set; } = [];

    public Dictionary<Guid, int> SubmissionCounts { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Forms = await db.FormDefinitions
            .Include(x => x.Versions)
                .ThenInclude(x => x.WorkflowDefinition)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        SubmissionCounts = await db.FormSubmissions
            .GroupBy(x => x.FormDefinitionId)
            .Select(x => new { FormDefinitionId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.FormDefinitionId, x => x.Count);
    }
}
