using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CMIForge.Pages;

public class SignupModel(
    CMIForgeDbContext db,
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

        if (!Input.AcceptLegalAgreement)
        {
            ModelState.AddModelError("Input.AcceptLegalAgreement", "You must accept the CMIForge terms and license agreement to submit a workspace request.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var acceptedByName = $"{Input.AdminFirstName.Trim()} {Input.AdminLastName.Trim()}".Trim();
        var request = new TenantProvisioningRequest
        {
            FirmName = Input.FirmName.Trim(),
            AdminFirstName = Input.AdminFirstName.Trim(),
            AdminLastName = Input.AdminLastName.Trim(),
            AdminEmail = Input.AdminEmail.Trim(),
            DesiredDomain = Input.DesiredDomain?.Trim() ?? string.Empty,
            DesiredSubdomain = Input.DesiredSubdomain?.Trim() ?? string.Empty,
            Plan = Input.Plan,
            Notes = Input.Notes?.Trim() ?? string.Empty,
            LegalAgreementAcceptance = new LegalAgreementAcceptance
            {
                CustomerName = Input.FirmName.Trim(),
                AcceptedByName = acceptedByName,
                AcceptedByEmail = Input.AdminEmail.Trim(),
                AgreementKey = LegalAgreementVersions.CurrentKey,
                AgreementVersion = LegalAgreementVersions.CurrentVersion,
                AgreementTitle = LegalAgreementVersions.CurrentTitle,
                ProductVersion = ProductInfo.DisplayVersion,
                Accepted = true,
                AcceptedAt = DateTimeOffset.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                UserAgent = Request.Headers.UserAgent.ToString()
            }
        };

        db.TenantProvisioningRequests.Add(request);
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "TenantSignup.Created",
            "TenantProvisioningRequest",
            request.Id,
            summary: $"New signup request from {request.FirmName}.",
            details: new
            {
                request.AdminEmail,
                request.Plan,
                request.DesiredDomain,
                request.DesiredSubdomain,
                AgreementAccepted = true,
                LegalAgreementVersions.CurrentKey,
                LegalAgreementVersions.CurrentVersion
            });

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

    [Display(Name = "Terms and license agreement")]
    public bool AcceptLegalAgreement { get; set; }
}
