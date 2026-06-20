using System.ComponentModel.DataAnnotations;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Contacts;

public class EditModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    CurrentUserService currentUserService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public ContactEditInput Input { get; set; } = new();

    public Contact? Contact { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        Contact = await db.Contacts.FirstOrDefaultAsync(x => x.Id == id);
        if (Contact is null)
        {
            return Page();
        }

        Input = ContactEditInput.FromContact(Contact);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        Contact = await db.Contacts.FirstOrDefaultAsync(x => x.Id == id);
        if (Contact is null)
        {
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var current = EntityChangeService.ToSnapshot(Contact);
        var proposed = Input.ToSnapshot();
        var summary = EntityChangeService.Summarize(current, proposed);
        if (summary.StartsWith("No field changes", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "No changes were detected.");
            return Page();
        }

        var actor = await currentUserService.GetCurrentUserAsync();
        var request = new EntityChangeRequest
        {
            EntityType = EntityChangeService.ContactEntityType,
            EntityId = Contact.Id,
            EntityNumber = Contact.ContactNumber.ToString("D8"),
            EntityName = Contact.DisplayName,
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
            "Contact",
            Contact.Id,
            Contact.ContactNumber.ToString("D8"),
            $"Submitted contact change request for {Contact.DisplayName}.",
            new { request.Id, request.Summary });

        return RedirectToPage("./Details", new { id = Contact.Id });
    }
}

public class ContactEditInput
{
    [Display(Name = "First name")]
    public string? FirstName { get; set; }

    [Display(Name = "Middle name")]
    public string? MiddleName { get; set; }

    [Display(Name = "Last name")]
    public string? LastName { get; set; }

    public string? Organization { get; set; }

    public string? Title { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public string? Phone { get; set; }

    [Display(Name = "Mobile phone")]
    public string? MobilePhone { get; set; }

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

    public static ContactEditInput FromContact(Contact contact)
    {
        return new ContactEditInput
        {
            FirstName = contact.FirstName,
            MiddleName = contact.MiddleName,
            LastName = contact.LastName,
            Organization = contact.Organization,
            Title = contact.Title,
            Email = contact.Email,
            Phone = contact.Phone,
            MobilePhone = contact.MobilePhone,
            AddressLine1 = contact.AddressLine1,
            AddressLine2 = contact.AddressLine2,
            City = contact.City,
            State = contact.State,
            PostalCode = contact.PostalCode,
            Country = contact.Country,
            Notes = contact.Notes
        };
    }

    public ContactChangeSnapshot ToSnapshot()
    {
        return new ContactChangeSnapshot
        {
            FirstName = FirstName?.Trim() ?? string.Empty,
            MiddleName = MiddleName?.Trim() ?? string.Empty,
            LastName = LastName?.Trim() ?? string.Empty,
            Organization = Organization?.Trim() ?? string.Empty,
            Title = Title?.Trim() ?? string.Empty,
            Email = Email?.Trim() ?? string.Empty,
            Phone = Phone?.Trim() ?? string.Empty,
            MobilePhone = MobilePhone?.Trim() ?? string.Empty,
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
