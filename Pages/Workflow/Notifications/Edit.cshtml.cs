using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CMIForge.Pages.Workflow.Notifications;

public partial class EditModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    ProductPlanService productPlanService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public NotificationTemplateInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!productPlanService.AllowsFeature(ProductFeatureKeys.Workflow))
        {
            return RedirectToPage("/Billing/Index", new { locked = ProductFeatureKeys.Workflow });
        }

        if (!await permissionService.HasAsync(PermissionKeys.WorkflowsDesign))
        {
            return Forbid();
        }

        var template = await db.WorkflowNotificationTemplates.FirstOrDefaultAsync(x => x.Id == Id);
        if (template is null)
        {
            return NotFound();
        }

        Input = new NotificationTemplateInput
        {
            Name = template.Name,
            Key = template.Key,
            Description = template.Description,
            Subject = template.Subject,
            Body = template.Body,
            IsActive = template.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!productPlanService.AllowsFeature(ProductFeatureKeys.Workflow))
        {
            return RedirectToPage("/Billing/Index", new { locked = ProductFeatureKeys.Workflow });
        }

        if (!await permissionService.HasAsync(PermissionKeys.WorkflowsDesign))
        {
            return Forbid();
        }

        var template = await db.WorkflowNotificationTemplates.FirstOrDefaultAsync(x => x.Id == Id);
        if (template is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var key = Input.Key.Trim();
        if (!SlugRegex().IsMatch(key))
        {
            ModelState.AddModelError("Input.Key", "Use lowercase letters, numbers, and hyphens only.");
            return Page();
        }

        var keyExists = await db.WorkflowNotificationTemplates.AnyAsync(x => x.Id != Id && x.Key == key);
        if (keyExists)
        {
            ModelState.AddModelError("Input.Key", "Another notification template already uses this key.");
            return Page();
        }

        template.Name = Input.Name.Trim();
        template.Key = key;
        template.Description = Input.Description?.Trim() ?? string.Empty;
        template.Subject = Input.Subject.Trim();
        template.Body = Input.Body.Trim();
        template.IsActive = Input.IsActive;
        template.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "WorkflowNotificationTemplate.Updated",
            "WorkflowNotificationTemplate",
            template.Id,
            template.Key,
            $"Updated notification template {template.Name}.",
            new { template.IsActive });

        return RedirectToPage("./Index");
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();
}
