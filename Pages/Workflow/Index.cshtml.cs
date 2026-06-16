using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatterForge.Pages.Workflow;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        return RedirectToPage("./Queue");
    }
}
