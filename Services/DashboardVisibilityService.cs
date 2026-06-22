using CMIForge.Data;
using CMIForge.Models;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public class DashboardVisibilityService(
    CMIForgeDbContext db,
    CurrentUserService currentUserService,
    PermissionService permissionService)
{
    public static IReadOnlyList<DashboardDefinition> Definitions { get; } =
    [
        new(DashboardKeys.Firm, "Firm Admin", "Firm-wide intake, workflow, conflict, time, and plan health."),
        new(DashboardKeys.User, "My Work", "Personal queue, submissions, time, and conflict escalations."),
        new(DashboardKeys.Partner, "Matter Partner", "Lead-matter health, approvals, submitted time, and conflict attention.")
    ];

    public static bool IsKnownDashboard(string dashboardKey)
    {
        return Definitions.Any(x => x.Key == dashboardKey);
    }

    public static DashboardDefinition? Find(string dashboardKey)
    {
        return Definitions.FirstOrDefault(x => x.Key == dashboardKey);
    }

    public async Task<List<DashboardDefinition>> GetVisibleDashboardsAsync()
    {
        var currentUser = await currentUserService.GetCurrentUserAsync();
        var assignments = await db.DashboardAssignments
            .AsNoTracking()
            .Where(x => Definitions.Select(definition => definition.Key).Contains(x.DashboardKey))
            .ToListAsync();

        if (currentUser is null)
        {
            return Definitions
                .Where(definition => !assignments.Any(x => x.DashboardKey == definition.Key))
                .ToList();
        }

        if (await permissionService.HasAsync(currentUser.Id, PermissionKeys.SystemAdmin))
        {
            return Definitions.ToList();
        }

        var teamIds = await db.TeamMembers
            .AsNoTracking()
            .Where(x => x.UserId == currentUser.Id && x.Team != null && x.Team.IsActive)
            .Select(x => x.TeamId)
            .ToListAsync();

        var directRoleIds = db.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == currentUser.Id && x.SecurityRole != null && x.SecurityRole.IsActive)
            .Select(x => x.SecurityRoleId);

        var teamRoleIds = db.TeamMembers
            .AsNoTracking()
            .Where(x => x.UserId == currentUser.Id && x.Team != null && x.Team.IsActive)
            .SelectMany(x => x.Team!.Roles)
            .Where(x => x.SecurityRole != null && x.SecurityRole.IsActive)
            .Select(x => x.SecurityRoleId);

        var roleIds = await directRoleIds
            .Concat(teamRoleIds)
            .Distinct()
            .ToListAsync();

        return Definitions
            .Where(definition => IsVisible(definition.Key, assignments, currentUser.Id, teamIds, roleIds))
            .ToList();
    }

    private static bool IsVisible(
        string dashboardKey,
        IReadOnlyCollection<DashboardAssignment> assignments,
        Guid userId,
        IReadOnlyCollection<Guid> teamIds,
        IReadOnlyCollection<Guid> roleIds)
    {
        var dashboardAssignments = assignments
            .Where(x => x.DashboardKey == dashboardKey)
            .ToList();
        if (dashboardAssignments.Count == 0)
        {
            return true;
        }

        return dashboardAssignments.Any(x =>
            x.UserId == userId ||
            (x.TeamId.HasValue && teamIds.Contains(x.TeamId.Value)) ||
            (x.SecurityRoleId.HasValue && roleIds.Contains(x.SecurityRoleId.Value)));
    }
}
