using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Security.Dashboards;

public class IndexModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    AuditLogService auditLogService) : PageModel
{
    private const string UserTarget = "user";
    private const string TeamTarget = "team";
    private const string RoleTarget = "role";

    public List<DashboardManagementRow> Dashboards { get; private set; } = [];

    public List<SelectListItem> TargetOptions { get; private set; } = [];

    [BindProperty]
    public string DashboardKey { get; set; } = string.Empty;

    [BindProperty]
    public string TargetReference { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAssignmentAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        var dashboard = DashboardVisibilityService.Find(DashboardKey);
        if (dashboard is null)
        {
            ModelState.AddModelError(nameof(DashboardKey), "Choose a valid dashboard.");
        }

        var target = ParseTargetReference(TargetReference);
        if (target is null)
        {
            ModelState.AddModelError(nameof(TargetReference), "Choose a user, team, or role.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var assignment = await BuildAssignmentAsync(DashboardKey, target!.Value);
        if (assignment is null)
        {
            ModelState.AddModelError(nameof(TargetReference), "Choose an active user, team, or role.");
            await LoadAsync();
            return Page();
        }

        if (!await AssignmentExistsAsync(assignment))
        {
            db.DashboardAssignments.Add(assignment);
            await db.SaveChangesAsync();
            await auditLogService.LogAsync(
                "DashboardAssignment.Added",
                "Dashboard",
                assignment.Id,
                dashboard!.Key,
                $"Added dashboard visibility for {dashboard.Name}.",
                new
                {
                    assignment.DashboardKey,
                    assignment.UserId,
                    assignment.TeamId,
                    assignment.SecurityRoleId
                });
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAssignmentAsync(Guid assignmentId)
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        var assignment = await db.DashboardAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId);
        if (assignment is not null)
        {
            var dashboard = DashboardVisibilityService.Find(assignment.DashboardKey);
            db.DashboardAssignments.Remove(assignment);
            await db.SaveChangesAsync();
            await auditLogService.LogAsync(
                "DashboardAssignment.Removed",
                "Dashboard",
                assignment.Id,
                assignment.DashboardKey,
                $"Removed dashboard visibility for {dashboard?.Name ?? assignment.DashboardKey}.",
                new
                {
                    assignment.DashboardKey,
                    assignment.UserId,
                    assignment.TeamId,
                    assignment.SecurityRoleId
                });
        }

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var assignments = await db.DashboardAssignments
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Team)
            .Include(x => x.SecurityRole)
            .OrderBy(x => x.DashboardKey)
            .ThenBy(x => x.User!.DisplayName)
            .ThenBy(x => x.Team!.Name)
            .ThenBy(x => x.SecurityRole!.Name)
            .ToListAsync();

        Dashboards = DashboardVisibilityService.Definitions
            .Select(definition => new DashboardManagementRow(
                definition,
                assignments
                    .Where(x => x.DashboardKey == definition.Key)
                    .Select(DashboardAssignmentItem.FromAssignment)
                    .OrderBy(x => x.TargetType)
                    .ThenBy(x => x.TargetName)
                    .ToList()))
            .ToList();

        await LoadTargetOptionsAsync();
    }

    private async Task LoadTargetOptionsAsync()
    {
        var userGroup = new SelectListGroup { Name = "Users" };
        var teamGroup = new SelectListGroup { Name = "Teams" };
        var roleGroup = new SelectListGroup { Name = "Roles" };

        TargetOptions = [];
        TargetOptions.AddRange(await db.Users
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsArchived)
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem(
                $"{x.DisplayName} ({x.Email})",
                $"{UserTarget}:{x.Id}",
                false,
                false)
            { Group = userGroup })
            .ToListAsync());
        TargetOptions.AddRange(await db.Teams
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(
                x.Name,
                $"{TeamTarget}:{x.Id}",
                false,
                false)
            { Group = teamGroup })
            .ToListAsync());
        TargetOptions.AddRange(await db.SecurityRoles
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(
                x.Name,
                $"{RoleTarget}:{x.Id}",
                false,
                false)
            { Group = roleGroup })
            .ToListAsync());
    }

    private static DashboardTarget? ParseTargetReference(string targetReference)
    {
        var parts = targetReference.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var targetId))
        {
            return null;
        }

        return parts[0] is UserTarget or TeamTarget or RoleTarget
            ? new DashboardTarget(parts[0], targetId)
            : null;
    }

    private async Task<DashboardAssignment?> BuildAssignmentAsync(string dashboardKey, DashboardTarget target)
    {
        var assignment = new DashboardAssignment { DashboardKey = dashboardKey };
        if (target.Type == UserTarget)
        {
            var exists = await db.Users.AnyAsync(x => x.Id == target.Id && x.IsActive && !x.IsArchived);
            if (!exists)
            {
                return null;
            }

            assignment.UserId = target.Id;
        }
        else if (target.Type == TeamTarget)
        {
            var exists = await db.Teams.AnyAsync(x => x.Id == target.Id && x.IsActive);
            if (!exists)
            {
                return null;
            }

            assignment.TeamId = target.Id;
        }
        else
        {
            var exists = await db.SecurityRoles.AnyAsync(x => x.Id == target.Id && x.IsActive);
            if (!exists)
            {
                return null;
            }

            assignment.SecurityRoleId = target.Id;
        }

        return assignment;
    }

    private async Task<bool> AssignmentExistsAsync(DashboardAssignment assignment)
    {
        return await db.DashboardAssignments.AnyAsync(x =>
            x.DashboardKey == assignment.DashboardKey &&
            x.UserId == assignment.UserId &&
            x.TeamId == assignment.TeamId &&
            x.SecurityRoleId == assignment.SecurityRoleId);
    }

    private readonly record struct DashboardTarget(string Type, Guid Id);
}

public sealed record DashboardManagementRow(
    DashboardDefinition Definition,
    List<DashboardAssignmentItem> Assignments);

public sealed record DashboardAssignmentItem(
    Guid Id,
    string TargetType,
    string TargetName,
    string TargetDetail)
{
    public static DashboardAssignmentItem FromAssignment(DashboardAssignment assignment)
    {
        if (assignment.User is not null)
        {
            return new DashboardAssignmentItem(
                assignment.Id,
                "User",
                assignment.User.DisplayName,
                assignment.User.Email);
        }

        if (assignment.Team is not null)
        {
            return new DashboardAssignmentItem(
                assignment.Id,
                "Team",
                assignment.Team.Name,
                assignment.Team.Description);
        }

        return new DashboardAssignmentItem(
            assignment.Id,
            "Role",
            assignment.SecurityRole?.Name ?? "Role",
            assignment.SecurityRole?.Description ?? string.Empty);
    }
}
