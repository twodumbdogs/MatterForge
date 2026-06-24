using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Users;

public class DetailsModel(
    CMIForgeDbContext db,
    DemoModeService demoModeService,
    PermissionService permissionService,
    EntityRelationshipService relationshipService,
    AuditLogService auditLogService) : PageModel
{
    public CMIForgeUser? UserRecord { get; private set; }

    public List<AuditLog> AuditHistory { get; private set; } = [];

    public List<EntityRelationshipDisplayRow> Relationships { get; private set; } = [];

    public List<SelectListItem> RelationshipTypeOptions { get; private set; } = [];

    public List<SelectListItem> RelationshipTargetTypeOptions { get; private set; } = [];

    public bool CanManageRelationships { get; private set; }

    public bool IsEditLocked => UserRecord is not null && demoModeService.IsProtectedSystemUser(UserRecord.SystemId);

    public string ProtectedUserMessage => demoModeService.ProtectedUserMessage;

    [TempData]
    public string? CreatedEntraUserPrincipalName { get; set; }

    [TempData]
    public string? CreatedEntraTemporaryPassword { get; set; }

    [BindProperty]
    public EntityRelationshipInput RelationshipInput { get; set; } = new()
    {
        TargetEntityType = EntityRelationshipEntityTypes.User
    };

    public async Task OnGetAsync(Guid id)
    {
        await LoadPageAsync(id);
    }

    public async Task<IActionResult> OnPostRelationshipAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        await LoadPageAsync(id);
        if (UserRecord is null)
        {
            return NotFound();
        }

        try
        {
            await relationshipService.AddRelationshipAsync(EntityRelationshipEntityTypes.User, id, RelationshipInput);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        await auditLogService.LogAsync(
            "User.RelationshipAdded",
            "User",
            id,
            UserRecord.SystemId.ToString("D8"),
            $"Added relationship for {UserRecord.DisplayName}.");

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteRelationshipAsync(Guid id, Guid relationshipId)
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        var deleted = await relationshipService.DeleteRelationshipAsync(EntityRelationshipEntityTypes.User, id, relationshipId);
        if (!deleted)
        {
            return NotFound();
        }

        await auditLogService.LogAsync(
            "User.RelationshipDeleted",
            "User",
            id,
            user.SystemId.ToString("D8"),
            $"Deleted relationship for {user.DisplayName}.");

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostArchiveAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        if (demoModeService.IsProtectedSystemUser(user.SystemId))
        {
            return RedirectToPage(new { id });
        }

        user.IsArchived = !user.IsArchived;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            user.IsArchived ? "User.Archived" : "User.Restored",
            "User",
            user.Id,
            user.SystemId.ToString("D8"),
            $"{(user.IsArchived ? "Archived" : "Restored")} user {user.DisplayName}.");

        return RedirectToPage(new { id });
    }

    private async Task LoadPageAsync(Guid id)
    {
        CanManageRelationships = await permissionService.HasAsync(PermissionKeys.SecurityManage);
        RelationshipTargetTypeOptions = relationshipService.GetTargetEntityTypeOptions(EntityRelationshipEntityTypes.User);
        RelationshipTypeOptions = await relationshipService.GetRelationshipTypeOptionsAsync(
            RelationshipScopes.EntityUser,
            RelationshipScopes.UserUser);

        UserRecord = await db.Users
            .Include(x => x.ResponsibleMatters)
                .ThenInclude(x => x.Client)
            .Include(x => x.LeadPartnerMatters)
                .ThenInclude(x => x.Client)
            .Include(x => x.TeamMemberships)
                .ThenInclude(x => x.Team)
            .Include(x => x.Roles)
                .ThenInclude(x => x.SecurityRole)
            .FirstOrDefaultAsync(x => x.Id == id);

        AuditHistory = UserRecord is null
            ? []
            : await auditLogService.ListForEntityAsync("User", id);

        Relationships = UserRecord is null
            ? []
            : await relationshipService.ListForEntityAsync(EntityRelationshipEntityTypes.User, id);
    }
}
