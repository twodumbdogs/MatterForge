using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatterForge.Pages.Forms;

public class SubmittedModel(PermissionService permissionService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int SubmissionNumber { get; set; }

    public bool CanViewSubmissions { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var canSubmitForms = await permissionService.HasAsync(PermissionKeys.FormsSubmit);
        CanViewSubmissions = await permissionService.HasAsync(PermissionKeys.SubmissionsViewOwn) ||
            await permissionService.HasAsync(PermissionKeys.SubmissionsViewAll);

        if (!canSubmitForms && !CanViewSubmissions)
        {
            return Forbid();
        }

        return Page();
    }
}
