using System.ComponentModel.DataAnnotations;
using MatterForge.Data;
using MatterForge.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Contacts;

public class CreateModel(MatterForgeDbContext db) : PageModel
{
    [BindProperty]
    public ContactInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var nextNumber = (await db.Contacts.MaxAsync(x => (int?)x.ContactNumber) ?? 0) + 1;
        var contact = new Contact
        {
            ContactNumber = nextNumber,
            FirstName = Input.FirstName?.Trim() ?? string.Empty,
            MiddleName = Input.MiddleName?.Trim() ?? string.Empty,
            LastName = Input.LastName?.Trim() ?? string.Empty,
            Organization = Input.Organization?.Trim() ?? string.Empty,
            Title = Input.Title?.Trim() ?? string.Empty,
            Email = Input.Email?.Trim() ?? string.Empty,
            Phone = Input.Phone?.Trim() ?? string.Empty,
            MobilePhone = Input.MobilePhone?.Trim() ?? string.Empty,
            AddressLine1 = Input.AddressLine1?.Trim() ?? string.Empty,
            AddressLine2 = Input.AddressLine2?.Trim() ?? string.Empty,
            City = Input.City?.Trim() ?? string.Empty,
            State = Input.State?.Trim() ?? string.Empty,
            PostalCode = Input.PostalCode?.Trim() ?? string.Empty,
            Country = Input.Country?.Trim() ?? string.Empty,
            Notes = Input.Notes?.Trim() ?? string.Empty
        };
        contact.DisplayName = BuildDisplayName(contact);

        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        return RedirectToPage("./Details", new { id = contact.Id });
    }

    private static string BuildDisplayName(Contact contact)
    {
        var personName = string.Join(" ", new[] { contact.FirstName, contact.MiddleName, contact.LastName }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (!string.IsNullOrWhiteSpace(personName))
        {
            return personName;
        }

        return string.IsNullOrWhiteSpace(contact.Organization)
            ? "Unnamed Contact"
            : contact.Organization;
    }
}

public class ContactInput
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
}
