using CMIForge.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CMIForge.Pages.System;

public class DemoBlockedModel(DemoModeService demoModeService) : PageModel
{
    public string Message { get; private set; } = string.Empty;

    public void OnGet(string reason = "action")
    {
        Message = reason.Equals("content", StringComparison.OrdinalIgnoreCase)
            ? demoModeService.BlockedContentMessage
            : demoModeService.BlockedActionMessage;
    }
}
