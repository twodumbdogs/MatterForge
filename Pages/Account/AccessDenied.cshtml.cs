using MatterForge.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatterForge.Pages.Account;

public class AccessDeniedModel(CurrentUserService currentUserService) : PageModel
{
    public string DisplayName { get; private set; } = "this account";

    public string? Email { get; private set; }

    public string? ReturnUrl { get; private set; }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        var currentUser = await currentUserService.GetCurrentUserAsync();
        if (currentUser is not null)
        {
            DisplayName = currentUser.DisplayName;
            Email = currentUser.Email;
            return;
        }

        Email = currentUserService.GetAuthenticatedEmail();
        if (!string.IsNullOrWhiteSpace(Email))
        {
            DisplayName = Email;
        }
    }
}
