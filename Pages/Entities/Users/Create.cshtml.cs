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
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public UserInput Input { get; set; } = new();

    public ProductLimitStatus UserLimit { get; private set; } = new("users", 0, null, true, string.Empty);

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

        if (!ModelState.IsValid)
        {
            return Page();
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
            new { user.Email, user.Title, user.IsActive });

        return RedirectToPage("./Index");
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
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Title / role")]
    public string? Title { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
