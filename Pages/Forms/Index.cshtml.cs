using CMIForge.Data;
using CMIForge.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Forms;

public class IndexModel(CMIForgeDbContext db) : PageModel
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
