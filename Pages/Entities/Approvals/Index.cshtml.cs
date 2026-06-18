using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Approvals;

public class IndexModel(
    MatterForgeDbContext db,
    PermissionService permissionService,
    CurrentUserService currentUserService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public string? ReviewNotes { get; set; }

    public List<EntityChangeRequest> PendingRequests { get; private set; } = [];

    public List<EntityChangeRequest> RecentReviewedRequests { get; private set; } = [];

    public bool CanApprove { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesView))
        {
            return Forbid();
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesApprove))
        {
            return Forbid();
        }

        var request = await db.EntityChangeRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (request is null || request.Status != EntityChangeRequestStatuses.Pending)
        {
            return RedirectToPage();
        }

        var actor = await currentUserService.GetCurrentUserAsync();
        if (request.EntityType == EntityChangeService.ClientEntityType)
        {
            var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == request.EntityId);
            var proposed = EntityChangeService.Deserialize<ClientChangeSnapshot>(request.ProposedValuesJson);
            if (client is null || proposed is null)
            {
                return RedirectToPage();
            }

            EntityChangeService.Apply(client, proposed);
        }
        else if (request.EntityType == EntityChangeService.MatterEntityType)
        {
            var matter = await db.Matters.FirstOrDefaultAsync(x => x.Id == request.EntityId);
            var proposed = EntityChangeService.Deserialize<MatterChangeSnapshot>(request.ProposedValuesJson);
            if (matter is null || proposed is null)
            {
                return RedirectToPage();
            }

            EntityChangeService.Apply(matter, proposed);
        }

        request.Status = EntityChangeRequestStatuses.Approved;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedByUserId = actor?.Id;
        request.ReviewNotes = ReviewNotes?.Trim() ?? string.Empty;

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "EntityChangeApproved",
            request.EntityType,
            request.EntityId,
            request.EntityNumber,
            $"Approved {request.EntityType.ToLowerInvariant()} change request for {request.EntityName}.",
            new { request.Id, request.Summary, request.ReviewNotes });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesApprove))
        {
            return Forbid();
        }

        var request = await db.EntityChangeRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (request is null || request.Status != EntityChangeRequestStatuses.Pending)
        {
            return RedirectToPage();
        }

        var actor = await currentUserService.GetCurrentUserAsync();
        request.Status = EntityChangeRequestStatuses.Rejected;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedByUserId = actor?.Id;
        request.ReviewNotes = ReviewNotes?.Trim() ?? string.Empty;

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "EntityChangeRejected",
            request.EntityType,
            request.EntityId,
            request.EntityNumber,
            $"Rejected {request.EntityType.ToLowerInvariant()} change request for {request.EntityName}.",
            new { request.Id, request.Summary, request.ReviewNotes });

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        CanApprove = await permissionService.HasAsync(PermissionKeys.EntitiesApprove);
        PendingRequests = await db.EntityChangeRequests
            .Include(x => x.RequestedByUser)
            .Where(x => x.Status == EntityChangeRequestStatuses.Pending)
            .OrderBy(x => x.RequestedAt)
            .ToListAsync();

        RecentReviewedRequests = await db.EntityChangeRequests
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewedByUser)
            .Where(x => x.Status != EntityChangeRequestStatuses.Pending)
            .OrderByDescending(x => x.ReviewedAt)
            .Take(20)
            .ToListAsync();
    }
}
