using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Workflow.Definitions;

public class IndexModel(
    MatterForgeDbContext db,
    PermissionService permissionService,
    ProductPlanService productPlanService) : PageModel
{
    public List<WorkflowDefinition> Workflows { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!productPlanService.AllowsFeature(ProductFeatureKeys.Workflow))
        {
            return RedirectToPage("/Billing/Index", new { locked = ProductFeatureKeys.Workflow });
        }

        if (!await permissionService.HasAsync(PermissionKeys.WorkflowsDesign))
        {
            return Forbid();
        }

        Workflows = await db.WorkflowDefinitions
            .Include(x => x.FormDefinition)
            .Include(x => x.Steps)
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Page();
    }
}
