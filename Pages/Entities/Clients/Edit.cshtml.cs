using System.ComponentModel.DataAnnotations;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Clients;

public class EditModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    CurrentUserService currentUserService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public ClientEditInput Input { get; set; } = new();

    public Client? Client { get; private set; }

    public string[] StatusOptions => EntityComplianceService.ClientStatusOptions;

    public bool IsOpeningFromComplianceReview { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        Client = await db.Clients.FirstOrDefaultAsync(x => x.Id == id);
        if (Client is null)
        {
            return Page();
        }

        Input = ClientEditInput.FromClient(Client);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        Client = await db.Clients.FirstOrDefaultAsync(x => x.Id == id);
        if (Client is null)
        {
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var current = EntityChangeService.ToSnapshot(Client);
        var proposed = Input.ToSnapshot();
        IsOpeningFromComplianceReview = EntityComplianceService.MovesFromReviewToCompliant(
            EntityChangeService.ClientEntityType,
            current.Status,
            proposed.Status);
        if (IsOpeningFromComplianceReview && string.IsNullOrWhiteSpace(Input.RequestNotes))
        {
            ModelState.AddModelError("Input.RequestNotes", "Add the compliance/checks note that supports moving this client to Active.");
            return Page();
        }

        var summary = EntityChangeService.Summarize(current, proposed);
        if (summary.StartsWith("No field changes", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "No changes were detected.");
            return Page();
        }

        var actor = await currentUserService.GetCurrentUserAsync();
        var request = new EntityChangeRequest
        {
            EntityType = EntityChangeService.ClientEntityType,
            EntityId = Client.Id,
            EntityNumber = Client.ClientNumber.ToString("D8"),
            EntityName = Client.Name,
            Summary = summary,
            CurrentValuesJson = EntityChangeService.Serialize(current),
            ProposedValuesJson = EntityChangeService.Serialize(proposed),
            RequestNotes = Input.RequestNotes?.Trim() ?? string.Empty,
            RequestedByUserId = actor?.Id
        };

        db.EntityChangeRequests.Add(request);
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "EntityChangeRequested",
            "Client",
            Client.Id,
            Client.ClientNumber.ToString("D8"),
            $"Submitted client change request for {Client.Name}.",
            new { request.Id, request.Summary });

        return RedirectToPage("./Details", new { id = Client.Id });
    }
}

public class ClientEditInput
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = EntityComplianceService.ReviewStatus;

    [Display(Name = "Primary contact")]
    public string? PrimaryContact { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public string? Phone { get; set; }

    [Display(Name = "Address line 1")]
    public string? AddressLine1 { get; set; }

    [Display(Name = "Address line 2")]
    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    [Display(Name = "Postal code")]
    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public string? Notes { get; set; }

    [Display(Name = "Reason for change")]
    public string? RequestNotes { get; set; }

    public static ClientEditInput FromClient(Client client)
    {
        return new ClientEditInput
        {
            Name = client.Name,
            Status = client.Status,
            PrimaryContact = client.PrimaryContact,
            Email = client.Email,
            Phone = client.Phone,
            AddressLine1 = client.AddressLine1,
            AddressLine2 = client.AddressLine2,
            City = client.City,
            State = client.State,
            PostalCode = client.PostalCode,
            Country = client.Country,
            Notes = client.Notes
        };
    }

    public ClientChangeSnapshot ToSnapshot()
    {
        return new ClientChangeSnapshot
        {
            Name = Name.Trim(),
            Status = Status.Trim(),
            PrimaryContact = PrimaryContact?.Trim() ?? string.Empty,
            Email = Email?.Trim() ?? string.Empty,
            Phone = Phone?.Trim() ?? string.Empty,
            AddressLine1 = AddressLine1?.Trim() ?? string.Empty,
            AddressLine2 = AddressLine2?.Trim() ?? string.Empty,
            City = City?.Trim() ?? string.Empty,
            State = State?.Trim() ?? string.Empty,
            PostalCode = PostalCode?.Trim() ?? string.Empty,
            Country = Country?.Trim() ?? string.Empty,
            Notes = Notes?.Trim() ?? string.Empty
        };
    }
}
