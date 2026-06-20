using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CMIForge.Pages.Api;

public class AddressLookupModel(AddressLookupService addressLookupService) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string? text, CancellationToken cancellationToken)
    {
        var response = await addressLookupService.SearchAsync(text, cancellationToken);
        return new JsonResult(response);
    }
}
