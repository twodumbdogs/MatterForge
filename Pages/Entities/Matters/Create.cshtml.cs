using System.ComponentModel.DataAnnotations;
using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Matters;

public class CreateModel(MatterForgeDbContext db, ProductPlanService productPlanService) : PageModel
{
    [BindProperty]
    public MatterInput Input { get; set; } = new();

    public List<SelectListItem> ClientOptions { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public ProductLimitStatus MatterLimit { get; private set; } = new("matters", 0, null, true, string.Empty);

    public async Task OnGetAsync()
    {
        Input.OpenedDate = DateOnly.FromDateTime(DateTime.Today);
        await LoadOptionsAsync();
        MatterLimit = await productPlanService.GetMatterLimitAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadOptionsAsync();
        MatterLimit = await productPlanService.GetMatterLimitAsync();

        if (!MatterLimit.CanCreate)
        {
            ModelState.AddModelError(string.Empty, MatterLimit.Message);
        }

        if (!await db.Clients.AnyAsync(x => x.Id == Input.ClientId))
        {
            ModelState.AddModelError("Input.ClientId", "Choose an existing client.");
        }

        if (Input.ResponsibleUserId.HasValue && !await db.Users.AnyAsync(x => x.Id == Input.ResponsibleUserId.Value))
        {
            ModelState.AddModelError("Input.ResponsibleUserId", "Choose an existing user.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var nextNumber = (await db.Matters.MaxAsync(x => (int?)x.MatterNumber) ?? 0) + 1;

        db.Matters.Add(new Matter
        {
            Name = Input.Name.Trim(),
            MatterNumber = nextNumber,
            ClientId = Input.ClientId,
            PracticeArea = Input.PracticeArea,
            Status = Input.Status,
            OpenedDate = Input.OpenedDate,
            ResponsibleUserId = Input.ResponsibleUserId,
            Notes = Input.Notes?.Trim() ?? string.Empty
        });

        await db.SaveChangesAsync();
        return RedirectToPage("./Index");
    }

    private async Task LoadOptionsAsync()
    {
        ClientOptions = await db.Clients
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();

        UserOptions = await db.Users
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem(x.DisplayName, x.Id.ToString()))
            .ToListAsync();
    }
}

public class MatterInput
{
    [Required]
    [Display(Name = "Matter name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Client")]
    public Guid ClientId { get; set; }

    [Display(Name = "Practice area")]
    public string PracticeArea { get; set; } = "Corporate";

    public string Status { get; set; } = "Open";

    [Display(Name = "Opened date")]
    public DateOnly? OpenedDate { get; set; }

    [Display(Name = "Responsible user")]
    public Guid? ResponsibleUserId { get; set; }

    public string? Notes { get; set; }
}
