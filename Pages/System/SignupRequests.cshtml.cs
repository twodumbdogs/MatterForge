using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.System;

public class SignupRequestsModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    CurrentUserService currentUserService,
    AuditLogService auditLogService) : PageModel
{
    public List<TenantProvisioningRequest> Requests { get; private set; } = [];

    public string[] StatusOptions { get; } =
    [
        TenantProvisioningStatuses.New,
        TenantProvisioningStatuses.Contacted,
        TenantProvisioningStatuses.Provisioning,
        TenantProvisioningStatuses.Ready,
        TenantProvisioningStatuses.Failed,
        TenantProvisioningStatuses.Closed
    ];

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid id, string status, string? internalNotes)
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        var request = await db.TenantProvisioningRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (request is null)
        {
            return NotFound();
        }

        if (!StatusOptions.Contains(status))
        {
            ModelState.AddModelError(string.Empty, "Choose a valid request status.");
            await LoadAsync();
            return Page();
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        var previousStatus = request.Status;
        request.Status = status;
        request.InternalNotes = internalNotes?.Trim() ?? string.Empty;
        request.UpdatedAt = DateTimeOffset.UtcNow;
        request.UpdatedByUserId = currentUser?.Id;

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "TenantSignup.Updated",
            "TenantProvisioningRequest",
            request.Id,
            summary: $"Updated signup request for {request.FirmName} from {previousStatus} to {request.Status}.",
            details: new { request.AdminEmail, request.Plan, previousStatus, request.Status });

        StatusMessage = $"Updated {request.FirmName}.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Requests = await db.TenantProvisioningRequests
            .Include(x => x.UpdatedByUser)
            .OrderBy(x => x.Status == TenantProvisioningStatuses.New ? 0 : 1)
            .ThenByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync();
    }
}
