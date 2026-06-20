using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CMIForge.Pages.Billing;

public class IndexModel(ProductPlanService productPlanService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Locked { get; set; }

    public ProductPlan CurrentPlan => productPlanService.CurrentPlan;

    public IReadOnlyList<ProductPlan> Plans => productPlanService.Plans;

    public ProductUsageSnapshot Usage { get; private set; } = new(0, 0, 0);

    public async Task OnGetAsync()
    {
        Usage = await productPlanService.GetUsageAsync();
    }

    public string LockedFeatureLabel()
    {
        return string.IsNullOrWhiteSpace(Locked)
            ? string.Empty
            : ProductFeatureKeys.Label(Locked);
    }
}
