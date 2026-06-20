using System.ComponentModel.DataAnnotations;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Matters;

public class CreateModel(
    CMIForgeDbContext db,
    ProductPlanService productPlanService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public MatterInput Input { get; set; } = new();

    public List<SelectListItem> ClientOptions { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public List<SelectListItem> PartnerOptions { get; private set; } = [];

    public List<SelectListItem> TimeIncrementOptions { get; private set; } = [];

    public List<SelectListItem> TimeCodeSetOptions { get; private set; } = [];

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

        if (Input.LeadPartnerId.HasValue && !await IsPartnerAsync(Input.LeadPartnerId.Value))
        {
            ModelState.AddModelError("Input.LeadPartnerId", "Choose a user with the Partner role.");
        }

        if (Input.RequiresTimeApproval && !Input.LeadPartnerId.HasValue)
        {
            ModelState.AddModelError("Input.LeadPartnerId", "Choose a lead partner before requiring time approval.");
        }

        if (Input.TimeIncrementMinutes.HasValue && !TimeIncrementRules.AllowedValues.Contains(Input.TimeIncrementMinutes.Value))
        {
            ModelState.AddModelError("Input.TimeIncrementMinutes", "Choose a valid time increment.");
        }

        if (Input.TimeCodeSetId.HasValue && !await db.TimeCodeSets.AnyAsync(x => x.Id == Input.TimeCodeSetId.Value && x.IsActive))
        {
            ModelState.AddModelError("Input.TimeCodeSetId", "Choose an active time code set.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var nextNumber = (await db.Matters.MaxAsync(x => (int?)x.MatterNumber) ?? 0) + 1;

        var matter = new Matter
        {
            Name = Input.Name.Trim(),
            MatterNumber = nextNumber,
            ClientId = Input.ClientId,
            PracticeArea = Input.PracticeArea,
            Status = Input.Status,
            OpenedDate = Input.OpenedDate,
            ResponsibleUserId = Input.ResponsibleUserId,
            LeadPartnerId = Input.LeadPartnerId,
            RequiresTimeApproval = Input.RequiresTimeApproval,
            TimeIncrementMinutes = Input.TimeIncrementMinutes,
            TimeCodeSetId = Input.TimeCodeSetId,
            Notes = Input.Notes?.Trim() ?? string.Empty
        };

        db.Matters.Add(matter);

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Matter.Created",
            "Matter",
            matter.Id,
            matter.MatterNumber.ToString("D8"),
            $"Created matter {matter.Name}.",
            new { matter.ClientId, matter.ResponsibleUserId, matter.LeadPartnerId });

        return RedirectToPage("./Index");
    }

    private async Task LoadOptionsAsync()
    {
        ClientOptions = await db.Clients
            .Where(x => !x.IsArchived)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();

        UserOptions = await db.Users
            .Where(x => x.IsActive && !x.IsArchived)
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem(x.DisplayName, x.Id.ToString()))
            .ToListAsync();

        TimeIncrementOptions =
        [
            new SelectListItem("Use system default", string.Empty),
            new SelectListItem("Actual time", TimeIncrementRules.ActualMinutes.ToString()),
            new SelectListItem("6-minute increments", TimeIncrementRules.SixMinutes.ToString()),
            new SelectListItem("15-minute increments", TimeIncrementRules.FifteenMinutes.ToString())
        ];

        TimeCodeSetOptions = await db.TimeCodeSets
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();

        PartnerOptions = await db.Users
            .Where(x => x.IsActive && !x.IsArchived)
            .Where(x => x.Roles.Any(role => role.SecurityRole != null && role.SecurityRole.Key == SecurityRoleKeys.Partner && role.SecurityRole.IsActive))
            .OrderBy(x => x.DisplayName)
            .Select(x => new SelectListItem(x.DisplayName, x.Id.ToString()))
            .ToListAsync();
    }

    private Task<bool> IsPartnerAsync(Guid userId)
    {
        return db.Users.AnyAsync(x =>
            x.Id == userId &&
            x.IsActive &&
            !x.IsArchived &&
            x.Roles.Any(role => role.SecurityRole != null && role.SecurityRole.Key == SecurityRoleKeys.Partner && role.SecurityRole.IsActive));
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

    [Display(Name = "Lead partner")]
    public Guid? LeadPartnerId { get; set; }

    [Display(Name = "Requires time approval")]
    public bool RequiresTimeApproval { get; set; }

    [Display(Name = "Time increment")]
    public int? TimeIncrementMinutes { get; set; }

    [Display(Name = "Time code set")]
    public Guid? TimeCodeSetId { get; set; }

    public string? Notes { get; set; }
}
