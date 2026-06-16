using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Users;

public class EditModel(
    MatterForgeDbContext db,
    DemoModeService demoModeService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public UserInput Input { get; set; } = new();

    public MatterForgeUser? UserRecord { get; private set; }

    public bool IsEditLocked { get; private set; }

    public string ProtectedUserMessage => demoModeService.ProtectedUserMessage;

    public async Task<IActionResult> OnGetAsync()
    {
        UserRecord = await db.Users.FirstOrDefaultAsync(x => x.Id == Id);
        if (UserRecord is null)
        {
            return Page();
        }

        IsEditLocked = demoModeService.IsProtectedSystemUser(UserRecord.SystemId);

        Input = new UserInput
        {
            FirstName = UserRecord.FirstName,
            MiddleName = UserRecord.MiddleName,
            LastName = UserRecord.LastName,
            Email = UserRecord.Email,
            Title = UserRecord.Title,
            IsActive = UserRecord.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        UserRecord = await db.Users.FirstOrDefaultAsync(x => x.Id == Id);
        if (UserRecord is null)
        {
            return Page();
        }

        IsEditLocked = demoModeService.IsProtectedSystemUser(UserRecord.SystemId);
        if (IsEditLocked)
        {
            ModelState.AddModelError(string.Empty, ProtectedUserMessage);
            return Page();
        }

        if (await db.Users.AnyAsync(x => x.Email == Input.Email && x.Id != Id))
        {
            ModelState.AddModelError("Input.Email", "A user with that email already exists.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var before = new
        {
            UserRecord.FirstName,
            UserRecord.MiddleName,
            UserRecord.LastName,
            UserRecord.DisplayName,
            UserRecord.Email,
            UserRecord.Title,
            UserRecord.IsActive
        };

        UserRecord.FirstName = Input.FirstName.Trim();
        UserRecord.MiddleName = Input.MiddleName?.Trim() ?? string.Empty;
        UserRecord.LastName = Input.LastName.Trim();
        UserRecord.DisplayName = UserNameParts.BuildDisplayName(Input.FirstName, Input.MiddleName, Input.LastName);
        UserRecord.Email = Input.Email.Trim();
        UserRecord.Title = Input.Title?.Trim() ?? string.Empty;
        UserRecord.IsActive = Input.IsActive;
        UserRecord.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "User.Updated",
            "User",
            UserRecord.Id,
            UserRecord.SystemId.ToString("D8"),
            $"Updated user {UserRecord.DisplayName}.",
            new
            {
                Before = before,
                After = new
                {
                    UserRecord.FirstName,
                    UserRecord.MiddleName,
                    UserRecord.LastName,
                    UserRecord.DisplayName,
                    UserRecord.Email,
                    UserRecord.Title,
                    UserRecord.IsActive
                }
            });

        return RedirectToPage("./Details", new { id = UserRecord.Id });
    }
}
