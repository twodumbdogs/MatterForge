using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Approvals;

public class IndexModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    CurrentUserService currentUserService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public string? ReviewNotes { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public List<EntityChangeRequest> PendingRequests { get; private set; } = [];

    public List<EntityChangeRequest> RecentReviewedRequests { get; private set; } = [];

    public RecordPage PendingPagination { get; private set; } = RecordPage.Empty;

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
        else if (request.EntityType == EntityChangeService.ContactEntityType)
        {
            var contact = await db.Contacts.FirstOrDefaultAsync(x => x.Id == request.EntityId);
            var proposed = EntityChangeService.Deserialize<ContactChangeSnapshot>(request.ProposedValuesJson);
            if (contact is null || proposed is null)
            {
                return RedirectToPage();
            }

            EntityChangeService.Apply(contact, proposed);
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

        return RedirectToPage(new { pageNumber = PageNumber, Search });
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

        return RedirectToPage(new { pageNumber = PageNumber, Search });
    }

    private async Task LoadAsync()
    {
        CanApprove = await permissionService.HasAsync(PermissionKeys.EntitiesApprove);

        var pendingQuery = db.EntityChangeRequests
            .Where(x => x.Status == EntityChangeRequestStatuses.Pending);

        Search = Search?.Trim();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            pendingQuery = pendingQuery.Where(x =>
                x.EntityType.Contains(Search) ||
                x.EntityNumber.Contains(Search) ||
                x.EntityName.Contains(Search) ||
                x.Summary.Contains(Search) ||
                x.RequestNotes.Contains(Search) ||
                (x.RequestedByUser != null && x.RequestedByUser.DisplayName.Contains(Search)));
        }

        PendingPagination = RecordPage.Create(PageNumber, await pendingQuery.CountAsync());
        PageNumber = PendingPagination.PageNumber;

        PendingRequests = await pendingQuery
            .Include(x => x.RequestedByUser)
            .OrderBy(x => x.RequestedAt)
            .Skip(PendingPagination.Skip)
            .Take(PendingPagination.PageSize)
            .ToListAsync();

        var recentReviewedQuery = db.EntityChangeRequests
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewedByUser)
            .Where(x => x.Status != EntityChangeRequestStatuses.Pending);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            recentReviewedQuery = recentReviewedQuery.Where(x =>
                x.Status.Contains(Search) ||
                x.EntityType.Contains(Search) ||
                x.EntityNumber.Contains(Search) ||
                x.EntityName.Contains(Search) ||
                x.Summary.Contains(Search) ||
                x.RequestNotes.Contains(Search) ||
                x.ReviewNotes.Contains(Search) ||
                (x.RequestedByUser != null && x.RequestedByUser.DisplayName.Contains(Search)) ||
                (x.ReviewedByUser != null && x.ReviewedByUser.DisplayName.Contains(Search)));
        }

        RecentReviewedRequests = await recentReviewedQuery
            .OrderByDescending(x => x.ReviewedAt)
            .Take(20)
            .ToListAsync();
    }
}
