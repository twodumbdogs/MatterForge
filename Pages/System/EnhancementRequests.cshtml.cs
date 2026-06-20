using System.ComponentModel.DataAnnotations;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.System;

public class EnhancementRequestsModel(
    CMIForgeDbContext db,
    CurrentUserService currentUserService,
    PermissionService permissionService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public EnhancementInput Input { get; set; } = new();

    public List<EnhancementRequest> Requests { get; private set; } = [];

    public bool CanManageRequests { get; private set; }

    public string[] AreaOptions { get; } =
    [
        EnhancementRequestAreas.General,
        EnhancementRequestAreas.IntakeForms,
        EnhancementRequestAreas.Conflicts,
        EnhancementRequestAreas.Entities,
        EnhancementRequestAreas.Workflow,
        EnhancementRequestAreas.Reporting,
        EnhancementRequestAreas.Notifications
    ];

    public string[] PriorityOptions { get; } =
    [
        EnhancementRequestPriorities.NiceToHave,
        EnhancementRequestPriorities.Useful,
        EnhancementRequestPriorities.Important,
        EnhancementRequestPriorities.Urgent
    ];

    public string[] StatusOptions { get; } =
    [
        EnhancementRequestStatuses.New,
        EnhancementRequestStatuses.Reviewing,
        EnhancementRequestStatuses.Planned,
        EnhancementRequestStatuses.Shipped,
        EnhancementRequestStatuses.Closed
    ];

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var currentUser = await currentUserService.GetCurrentUserAsync();
        if (currentUser is null)
        {
            return Forbid();
        }

        if (!AreaOptions.Contains(Input.Area))
        {
            ModelState.AddModelError("Input.Area", "Choose a valid product area.");
        }

        if (!PriorityOptions.Contains(Input.Priority))
        {
            ModelState.AddModelError("Input.Priority", "Choose a valid priority.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var request = new EnhancementRequest
        {
            Title = Input.Title.Trim(),
            Area = Input.Area,
            Priority = Input.Priority,
            Description = Input.Description.Trim(),
            BusinessValue = Input.BusinessValue?.Trim() ?? string.Empty,
            SubmittedByUserId = currentUser.Id,
            SubmittedByDisplayName = currentUser.DisplayName,
            SubmittedByEmail = currentUser.Email
        };

        db.EnhancementRequests.Add(request);
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "EnhancementRequest.Created",
            "EnhancementRequest",
            request.Id,
            summary: $"Submitted enhancement request: {request.Title}.",
            details: new { request.Area, request.Priority, request.SubmittedByEmail });

        StatusMessage = "Enhancement request submitted. Thank you for the good brain fuel.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid id, string status, string? internalNotes)
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        if (!StatusOptions.Contains(status))
        {
            ModelState.AddModelError(string.Empty, "Choose a valid request status.");
            await LoadAsync();
            return Page();
        }

        var request = await db.EnhancementRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (request is null)
        {
            return NotFound();
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        var previousStatus = request.Status;
        request.Status = status;
        request.InternalNotes = internalNotes?.Trim() ?? string.Empty;
        request.UpdatedAt = DateTimeOffset.UtcNow;
        request.UpdatedByUserId = currentUser?.Id;

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "EnhancementRequest.Updated",
            "EnhancementRequest",
            request.Id,
            summary: $"Updated enhancement request from {previousStatus} to {request.Status}: {request.Title}.",
            details: new { request.Area, request.Priority, previousStatus, request.Status });

        StatusMessage = $"Updated {request.Title}.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        CanManageRequests = await permissionService.HasAsync(PermissionKeys.SecurityManage);
        var currentUser = await currentUserService.GetCurrentUserAsync();

        var query = db.EnhancementRequests
            .AsNoTracking()
            .Include(x => x.SubmittedByUser)
            .Include(x => x.UpdatedByUser)
            .AsQueryable();

        if (!CanManageRequests)
        {
            query = currentUser is null
                ? query.Where(x => false)
                : query.Where(x => x.SubmittedByUserId == currentUser.Id);
        }

        Requests = await query
            .OrderBy(x => x.Status == EnhancementRequestStatuses.New ? 0 : 1)
            .ThenByDescending(x => x.CreatedAt)
            .Take(CanManageRequests ? 200 : 25)
            .ToListAsync();
    }

    public class EnhancementInput
    {
        [Required]
        [StringLength(160)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(80)]
        public string Area { get; set; } = EnhancementRequestAreas.General;

        [Required]
        [StringLength(40)]
        public string Priority { get; set; } = EnhancementRequestPriorities.NiceToHave;

        [Required]
        [StringLength(4000)]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Business value")]
        [StringLength(1000)]
        public string? BusinessValue { get; set; }
    }
}
