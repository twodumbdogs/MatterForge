using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatterForge.Pages;

public class SignupModel(
    MatterForgeDbContext db,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public SignupInput Input { get; set; } = new();

    [TempData]
    public string? SignupMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!IsValidEmail(Input.AdminEmail))
        {
            ModelState.AddModelError("Input.AdminEmail", "Use a valid admin email address.");
        }

        if (!ValidPlans.Contains(Input.Plan))
        {
            ModelState.AddModelError("Input.Plan", "Choose a valid plan.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new TenantProvisioningRequest
        {
            FirmName = Input.FirmName.Trim(),
            AdminFirstName = Input.AdminFirstName.Trim(),
            AdminLastName = Input.AdminLastName.Trim(),
            AdminEmail = Input.AdminEmail.Trim(),
            DesiredDomain = Input.DesiredDomain?.Trim() ?? string.Empty,
            DesiredSubdomain = Input.DesiredSubdomain?.Trim() ?? string.Empty,
            Plan = Input.Plan,
            Notes = Input.Notes?.Trim() ?? string.Empty
        };

        db.TenantProvisioningRequests.Add(request);
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "TenantSignup.Created",
            "TenantProvisioningRequest",
            request.Id,
            summary: $"New signup request from {request.FirmName}.",
            details: new { request.AdminEmail, request.Plan, request.DesiredDomain, request.DesiredSubdomain });

        SignupMessage = "Thanks. Your CMIForge request has been received, and we will follow up from support@cmiforge.com.";
        return RedirectToPage();
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            _ = new MailAddress(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static readonly string[] ValidPlans =
    [
        TenantProvisioningPlans.Community,
        TenantProvisioningPlans.Professional,
        TenantProvisioningPlans.Enterprise
    ];
}

public class SignupInput
{
    [Required]
    [Display(Name = "Firm name")]
    public string FirmName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Admin first name")]
    public string AdminFirstName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Admin last name")]
    public string AdminLastName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Admin email")]
    public string AdminEmail { get; set; } = string.Empty;

    [Display(Name = "Firm domain")]
    public string? DesiredDomain { get; set; }

    [Display(Name = "Preferred CMIForge subdomain")]
    public string? DesiredSubdomain { get; set; }

    [Required]
    public string Plan { get; set; } = TenantProvisioningPlans.Community;

    public string? Notes { get; set; }
}
