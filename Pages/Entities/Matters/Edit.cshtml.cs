using System.ComponentModel.DataAnnotations;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Matters;

public class EditModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    CurrentUserService currentUserService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public MatterEditInput Input { get; set; } = new();

    public Matter? Matter { get; private set; }

    public List<SelectListItem> ClientOptions { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public List<SelectListItem> PartnerOptions { get; private set; } = [];

    public List<SelectListItem> TimeIncrementOptions { get; private set; } = [];

    public List<SelectListItem> TimeCodeSetOptions { get; private set; } = [];

    public string[] StatusOptions => EntityComplianceService.MatterStatusOptions;

    public bool IsOpeningFromComplianceReview { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        await LoadOptionsAsync();
        Matter = await db.Matters
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (Matter is null)
        {
            return Page();
        }

        Input = MatterEditInput.FromMatter(Matter);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        await LoadOptionsAsync();
        Matter = await db.Matters
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (Matter is null)
        {
            return Page();
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

        var current = EntityChangeService.ToSnapshot(Matter);
        var proposed = Input.ToSnapshot();
        IsOpeningFromComplianceReview = EntityComplianceService.MovesFromReviewToCompliant(
            EntityChangeService.MatterEntityType,
            current.Status,
            proposed.Status);
        if (IsOpeningFromComplianceReview && string.IsNullOrWhiteSpace(Input.RequestNotes))
        {
            ModelState.AddModelError("Input.RequestNotes", "Add the compliance/checks note that supports moving this matter to Open.");
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
            EntityType = EntityChangeService.MatterEntityType,
            EntityId = Matter.Id,
            EntityNumber = Matter.MatterNumber.ToString("D8"),
            EntityName = Matter.Name,
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
            "Matter",
            Matter.Id,
            Matter.MatterNumber.ToString("D8"),
            $"Submitted matter change request for {Matter.Name}.",
            new { request.Id, request.Summary });

        return RedirectToPage("./Details", new { id = Matter.Id });
    }

    private async Task LoadOptionsAsync()
    {
        ClientOptions = await db.Clients
            .Where(x => !x.IsArchived)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.ClientNumber:D8} - {x.Name}", x.Id.ToString()))
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

public class MatterEditInput
{
    [Required]
    [Display(Name = "Matter name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Client")]
    public Guid ClientId { get; set; }

    [Display(Name = "Practice area")]
    public string PracticeArea { get; set; } = "Corporate";

    public string Status { get; set; } = EntityComplianceService.ReviewStatus;

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

    [Display(Name = "Reason for change")]
    public string? RequestNotes { get; set; }

    public static MatterEditInput FromMatter(Matter matter)
    {
        return new MatterEditInput
        {
            Name = matter.Name,
            ClientId = matter.ClientId,
            PracticeArea = matter.PracticeArea,
            Status = matter.Status,
            OpenedDate = matter.OpenedDate,
            ResponsibleUserId = matter.ResponsibleUserId,
            LeadPartnerId = matter.LeadPartnerId,
            RequiresTimeApproval = matter.RequiresTimeApproval,
            TimeIncrementMinutes = matter.TimeIncrementMinutes,
            TimeCodeSetId = matter.TimeCodeSetId,
            Notes = matter.Notes
        };
    }

    public MatterChangeSnapshot ToSnapshot()
    {
        return new MatterChangeSnapshot
        {
            Name = Name.Trim(),
            ClientId = ClientId,
            PracticeArea = PracticeArea.Trim(),
            Status = Status.Trim(),
            OpenedDate = OpenedDate,
            ResponsibleUserId = ResponsibleUserId,
            LeadPartnerId = LeadPartnerId,
            RequiresTimeApproval = RequiresTimeApproval,
            TimeIncrementMinutes = TimeIncrementMinutes,
            TimeCodeSetId = TimeCodeSetId,
            Notes = Notes?.Trim() ?? string.Empty
        };
    }
}
