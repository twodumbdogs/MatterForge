using System.ComponentModel.DataAnnotations;
using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Clients;

public class CreateModel(MatterForgeDbContext db, ProductPlanService productPlanService) : PageModel
{
    [BindProperty]
    public ClientInput Input { get; set; } = new();

    public ProductLimitStatus ClientLimit { get; private set; } = new("clients", 0, null, true, string.Empty);

    public async Task OnGetAsync()
    {
        ClientLimit = await productPlanService.GetClientLimitAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ClientLimit = await productPlanService.GetClientLimitAsync();

        if (!ClientLimit.CanCreate)
        {
            ModelState.AddModelError(string.Empty, ClientLimit.Message);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var nextNumber = (await db.Clients.MaxAsync(x => (int?)x.ClientNumber) ?? 0) + 1;

        db.Clients.Add(new Client
        {
            Name = Input.Name.Trim(),
            ClientNumber = nextNumber,
            Status = Input.Status,
            PrimaryContact = Input.PrimaryContact?.Trim() ?? string.Empty,
            Email = Input.Email?.Trim() ?? string.Empty,
            Phone = Input.Phone?.Trim() ?? string.Empty,
            AddressLine1 = Input.AddressLine1?.Trim() ?? string.Empty,
            AddressLine2 = Input.AddressLine2?.Trim() ?? string.Empty,
            City = Input.City?.Trim() ?? string.Empty,
            State = Input.State?.Trim() ?? string.Empty,
            PostalCode = Input.PostalCode?.Trim() ?? string.Empty,
            Country = Input.Country?.Trim() ?? string.Empty,
            Notes = Input.Notes?.Trim() ?? string.Empty
        });

        await db.SaveChangesAsync();
        return RedirectToPage("./Index");
    }
}

public class ClientInput
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = "Active";

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
}
