using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Security.Teams;

public partial class IndexModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    DemoModeService demoModeService,
    AuditLogService auditLogService) : PageModel
{
    public List<Team> Teams { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public List<SelectListItem> RoleOptions { get; private set; } = [];

    public bool IsDemoMode => demoModeService.IsEnabled;

    public string ProtectedUserMessage => demoModeService.ProtectedUserMessage;

    [BindProperty]
    public CreateTeamInput CreateTeam { get; set; } = new();

    [BindProperty]
    public Guid MemberUserId { get; set; }

    [BindProperty]
    public Guid RoleId { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateTeamAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        if (!SlugRegex().IsMatch(CreateTeam.Key ?? string.Empty))
        {
            ModelState.AddModelError("CreateTeam.Key", "Use lowercase letters, numbers, and hyphens only.");
        }

        if (!string.IsNullOrWhiteSpace(CreateTeam.Key) && await db.Teams.AnyAsync(x => x.Key == CreateTeam.Key.Trim()))
        {
            ModelState.AddModelError("CreateTeam.Key", "Another team already uses this key.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var team = new Team
        {
            Name = CreateTeam.Name.Trim(),
            Key = CreateTeam.Key!.Trim(),
            Description = CreateTeam.Description?.Trim() ?? string.Empty
        };

        db.Teams.Add(team);

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Team.Created",
            "Team",
            team.Id,
            team.Key,
            $"Created team {team.Name}.",
            new { team.Description });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddMemberAsync(Guid teamId)
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        var exists = await db.TeamMembers.AnyAsync(x => x.TeamId == teamId && x.UserId == MemberUserId);
        if (!exists && MemberUserId != Guid.Empty)
        {
            var member = await db.Users.FirstOrDefaultAsync(x => x.Id == MemberUserId);
            if (member is null)
            {
                return RedirectToPage();
            }

            if (demoModeService.IsProtectedSystemUser(member.SystemId))
            {
                ModelState.AddModelError(string.Empty, ProtectedUserMessage);
                await LoadAsync();
                return Page();
            }

            db.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId,
                UserId = MemberUserId
            });

            await db.SaveChangesAsync();
            var team = await db.Teams.FirstOrDefaultAsync(x => x.Id == teamId);
            await auditLogService.LogAsync(
                "TeamMember.Added",
                "Team",
                teamId,
                team?.Key,
                $"Added {member.DisplayName} to {team?.Name ?? "team"}.",
                new { MemberSystemId = member.SystemId, member.Email });
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddRoleAsync(Guid teamId)
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        var exists = await db.TeamRoles.AnyAsync(x => x.TeamId == teamId && x.SecurityRoleId == RoleId);
        if (!exists && RoleId != Guid.Empty)
        {
            db.TeamRoles.Add(new TeamRole
            {
                TeamId = teamId,
                SecurityRoleId = RoleId
            });

            await db.SaveChangesAsync();
            var team = await db.Teams.FirstOrDefaultAsync(x => x.Id == teamId);
            var role = await db.SecurityRoles.FirstOrDefaultAsync(x => x.Id == RoleId);
            await auditLogService.LogAsync(
                "TeamRole.Added",
                "Team",
                teamId,
                team?.Key,
                $"Added role {role?.Name ?? "role"} to {team?.Name ?? "team"}.",
                new { RoleKey = role?.Key });
        }

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Teams = await db.Teams
            .Include(x => x.Members)
                .ThenInclude(x => x.User)
            .Include(x => x.Roles)
                .ThenInclude(x => x.SecurityRole)
            .OrderBy(x => x.Name)
            .ToListAsync();

        UserOptions = await db.Users
            .Where(x => x.IsActive && !x.IsArchived)
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem($"{x.DisplayName} ({x.SystemId:D8})", x.Id.ToString()))
            .ToListAsync();

        RoleOptions = await db.SecurityRoles
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();
}

public class CreateTeamInput
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string? Key { get; set; }

    public string? Description { get; set; }
}
