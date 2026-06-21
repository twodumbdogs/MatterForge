using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CMIForge.Pages.External.Forms;

public class SubmittedModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int SubmissionNumber { get; set; }
}
