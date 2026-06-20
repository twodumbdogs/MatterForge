using CMIForge.Data;
using CMIForge.Models;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public class PermissionService(CMIForgeDbContext db, CurrentUserService currentUserService)
{
    public async Task<CMIForgeUser?> GetCurrentUserAsync()
    {
        return await currentUserService.GetCurrentUserAsync();
    }

    public async Task<bool> HasAsync(string permissionKey)
    {
        var currentUser = await currentUserService.GetCurrentUserAsync();
        if (currentUser is null)
        {
            return false;
        }

        return await HasAsync(currentUser.Id, permissionKey);
    }

    public async Task<bool> HasAsync(Guid userId, string permissionKey)
    {
        var directPermissionKeys = db.UserRoles
            .Where(x => x.UserId == userId && x.SecurityRole != null && x.SecurityRole.IsActive)
            .SelectMany(x => x.SecurityRole!.RolePermissions)
            .Select(x => x.Permission!.Key);

        var teamPermissionKeys = db.TeamMembers
            .Where(x => x.UserId == userId && x.Team != null && x.Team.IsActive)
            .SelectMany(x => x.Team!.Roles)
            .Where(x => x.SecurityRole != null && x.SecurityRole.IsActive)
            .SelectMany(x => x.SecurityRole!.RolePermissions)
            .Select(x => x.Permission!.Key);

        return await directPermissionKeys
            .Concat(teamPermissionKeys)
            .AnyAsync(x => x == PermissionKeys.SystemAdmin || x == permissionKey);
    }

    public async Task<bool> CanActOnTaskAsync(SubmissionWorkflowTask task)
    {
        var currentUser = await currentUserService.GetCurrentUserAsync();
        if (currentUser is null)
        {
            return false;
        }

        if (await HasAsync(currentUser.Id, PermissionKeys.SystemAdmin) ||
            await HasAsync(currentUser.Id, PermissionKeys.WorkflowsViewAllQueues))
        {
            return true;
        }

        if (task.AssignedUserId == currentUser.Id)
        {
            return true;
        }

        if (!task.AssignedTeamId.HasValue)
        {
            return false;
        }

        var teamIds = await currentUserService.GetCurrentUserTeamIdsAsync();
        return teamIds.Contains(task.AssignedTeamId.Value);
    }
}
