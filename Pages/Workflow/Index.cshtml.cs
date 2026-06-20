using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CMIForge.Pages.Workflow;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        return RedirectToPage("./Queue");
    }
}
