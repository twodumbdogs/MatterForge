using System.ComponentModel.DataAnnotations;
using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Users;

public class CreateModel(
    MatterForgeDbContext db,
    ProductPlanService productPlanService,
    AuditLogService auditLogService,
    EntraUserProvisioningService entraUserProvisioningService) : PageModel
{
    [BindProperty]
    public UserInput Input { get; set; } = new();

    public ProductLimitStatus UserLimit { get; private set; } = new("users", 0, null, true, string.Empty);

    public bool EntraProvisioningConfigured => entraUserProvisioningService.IsConfigured;

    public string EntraDomain => entraUserProvisioningService.Domain;

    [TempData]
    public string? CreatedEntraUserPrincipalName { get; set; }

    [TempData]
    public string? CreatedEntraTemporaryPassword { get; set; }

    public async Task OnGetAsync()
    {
        UserLimit = await productPlanService.GetUserLimitAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        UserLimit = await productPlanService.GetUserLimitAsync();
        if (!UserLimit.CanCreate)
        {
            ModelState.AddModelError(string.Empty, UserLimit.Message);
        }

        if (await db.Users.AnyAsync(x => x.Email == Input.Email))
        {
            ModelState.AddModelError("Input.Email", "A user with that email already exists.");
        }

        string? entraUserPrincipalName = null;
        EntraCreateUserResult? entraResult = null;
        if (Input.CreateEntraLogin)
        {
            if (!EntraProvisioningConfigured)
            {
                ModelState.AddModelError("Input.CreateEntraLogin", "Entra user provisioning is not configured for this tenant yet.");
            }
            else if (string.IsNullOrWhiteSpace(Input.EntraUserName))
            {
                ModelState.AddModelError("Input.EntraUserName", "Enter the Entra login username.");
            }
            else if (!EntraUserProvisioningService.IsValidUserName(Input.EntraUserName))
            {
                ModelState.AddModelError("Input.EntraUserName", "Use letters, numbers, periods, underscores, or hyphens.");
            }
            else
            {
                entraUserPrincipalName = entraUserProvisioningService.BuildUserPrincipalName(Input.EntraUserName);
                if (await db.Users.AnyAsync(x => x.EntraUserPrincipalName == entraUserPrincipalName))
                {
                    ModelState.AddModelError("Input.EntraUserName", "A user with that Entra login already exists in CMIForge.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Input.CreateEntraLogin)
        {
            try
            {
                entraResult = await entraUserProvisioningService.CreateUserAsync(
                    new EntraCreateUserRequest(
                        Input.FirstName.Trim(),
                        Input.MiddleName?.Trim() ?? string.Empty,
                        Input.LastName.Trim(),
                        UserNameParts.BuildDisplayName(Input.FirstName, Input.MiddleName, Input.LastName),
                        Input.EntraUserName!,
                        Input.Title?.Trim()),
                    HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("Input.CreateEntraLogin", ex.Message);
                return Page();
            }
        }

        var nextSystemId = (await db.Users.MaxAsync(x => (int?)x.SystemId) ?? 0) + 1;

        var user = new MatterForgeUser
        {
            SystemId = nextSystemId,
            FirstName = Input.FirstName.Trim(),
            MiddleName = Input.MiddleName?.Trim() ?? string.Empty,
            LastName = Input.LastName.Trim(),
            DisplayName = UserNameParts.BuildDisplayName(Input.FirstName, Input.MiddleName, Input.LastName),
            Email = Input.Email.Trim(),
            EntraTenantId = entraResult?.TenantId ?? string.Empty,
            EntraObjectId = entraResult?.ObjectId ?? string.Empty,
            EntraUserPrincipalName = entraResult?.UserPrincipalName ?? entraUserPrincipalName ?? string.Empty,
            Title = Input.Title?.Trim() ?? string.Empty,
            IsActive = Input.IsActive
        };

        db.Users.Add(user);

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "User.Created",
            "User",
            user.Id,
            user.SystemId.ToString("D8"),
            $"Created user {user.DisplayName}.",
            new { user.Email, user.EntraUserPrincipalName, user.Title, user.IsActive });

        if (entraResult is not null)
        {
            CreatedEntraUserPrincipalName = entraResult.UserPrincipalName;
            CreatedEntraTemporaryPassword = entraResult.TemporaryPassword;
        }

        return RedirectToPage("./Details", new { id = user.Id });
    }
}

public class UserInput
{
    [Required]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Middle name")]
    public string? MiddleName { get; set; }

    [Required]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Work/contact email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Create Entra login")]
    public bool CreateEntraLogin { get; set; }

    [StringLength(64)]
    [Display(Name = "Login username")]
    public string? EntraUserName { get; set; }

    [Display(Name = "Title / role")]
    public string? Title { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
