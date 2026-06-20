using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Workflow.Notifications;

public partial class CreateModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    ProductPlanService productPlanService,
    AuditLogService auditLogService) : PageModel
{
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

        await ValidateInputAsync(null);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var template = new WorkflowNotificationTemplate
        {
            Name = Input.Name.Trim(),
            Key = Input.Key.Trim(),
            Description = Input.Description?.Trim() ?? string.Empty,
            Subject = Input.Subject.Trim(),
            Body = Input.Body.Trim(),
            IsActive = Input.IsActive
        };

        db.WorkflowNotificationTemplates.Add(template);
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "WorkflowNotificationTemplate.Created",
            "WorkflowNotificationTemplate",
            template.Id,
            template.Key,
            $"Created notification template {template.Name}.",
            new { template.IsActive });

        return RedirectToPage("./Index");
    }

    private async Task ValidateInputAsync(Guid? existingTemplateId)
    {
        if (!string.IsNullOrWhiteSpace(Input.Key) && !SlugRegex().IsMatch(Input.Key))
        {
            ModelState.AddModelError("Input.Key", "Use lowercase letters, numbers, and hyphens only.");
        }

        if (!string.IsNullOrWhiteSpace(Input.Key))
        {
            var key = Input.Key.Trim();
            var keyExists = await db.WorkflowNotificationTemplates.AnyAsync(x => x.Id != existingTemplateId && x.Key == key);
            if (keyExists)
            {
                ModelState.AddModelError("Input.Key", "Another notification template already uses this key.");
            }
        }
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();
}

public class NotificationTemplateInput
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Key { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
