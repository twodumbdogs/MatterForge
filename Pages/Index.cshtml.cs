using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages;

public class IndexModel(MatterForgeDbContext db, ProductPlanService productPlanService) : PageModel
{
    public int FormCount { get; private set; }

    public int SubmissionCount { get; private set; }

    public int PublishedVersionCount { get; private set; }

    public int TimeEntryCount { get; private set; }

    public decimal TimeHours { get; private set; }

    public List<FormDefinition> LatestForms { get; private set; } = [];

    public ProductPlan CurrentPlan => productPlanService.CurrentPlan;

    public ProductUsageSnapshot Usage { get; private set; } = new(0, 0, 0);

    public async Task OnGetAsync()
    {
        FormCount = await db.FormDefinitions.CountAsync(x => x.IsActive);
        SubmissionCount = await db.FormSubmissions.CountAsync();
        PublishedVersionCount = await db.FormVersions.CountAsync(x => x.IsPublished);
        TimeEntryCount = await db.TimeEntries.CountAsync();
        TimeHours = await db.TimeEntries.SumAsync(x => (decimal?)x.Minutes) / 60m ?? 0m;
        Usage = await productPlanService.GetUsageAsync();
        LatestForms = await db.FormDefinitions
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .ToListAsync();
    }
}
