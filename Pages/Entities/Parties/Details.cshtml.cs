using System.ComponentModel.DataAnnotations;
using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Entities.Parties;

public class DetailsModel(MatterForgeDbContext db) : PageModel
{
    public Party? Party { get; private set; }

    public List<SelectListItem> PartyOptions { get; private set; } = [];

    public List<SelectListItem> MatterOptions { get; private set; } = [];

    public SelectList RelationshipTypeOptions { get; } = new(PartyRelationshipTypes.All);

    public SelectList PartyRoleOptions { get; } = new(PartyRoles.All);

    [BindProperty]
    public AddAliasInput AliasInput { get; set; } = new();

    [BindProperty]
    public AddRelationshipInput RelationshipInput { get; set; } = new();

    [BindProperty]
    public AddMatterPartyInput MatterPartyInput { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        await LoadPageAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostAliasAsync(Guid id)
    {
        await LoadPageAsync(id);
        if (Party is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var normalizedAlias = ConflictSearchService.NormalizeName(AliasInput.Alias);
        var exists = await db.PartyAliases.AnyAsync(x => x.PartyId == id && x.NormalizedAlias == normalizedAlias);
        if (!exists)
        {
            db.PartyAliases.Add(new PartyAlias
            {
                PartyId = id,
                Alias = AliasInput.Alias.Trim(),
                NormalizedAlias = normalizedAlias,
                Notes = AliasInput.Notes?.Trim() ?? string.Empty
            });
            await db.SaveChangesAsync();
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRelationshipAsync(Guid id)
    {
        await LoadPageAsync(id);
        if (Party is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid || RelationshipInput.ToPartyId == id)
        {
            return Page();
        }

        var exists = await db.PartyRelationships.AnyAsync(x =>
            x.FromPartyId == id &&
            x.ToPartyId == RelationshipInput.ToPartyId &&
            x.RelationshipType == RelationshipInput.RelationshipType);

        if (!exists)
        {
            db.PartyRelationships.Add(new PartyRelationship
            {
                FromPartyId = id,
                ToPartyId = RelationshipInput.ToPartyId,
                RelationshipType = RelationshipInput.RelationshipType,
                Notes = RelationshipInput.Notes?.Trim() ?? string.Empty
            });
            await db.SaveChangesAsync();
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostMatterPartyAsync(Guid id)
    {
        await LoadPageAsync(id);
        if (Party is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var exists = await db.MatterParties.AnyAsync(x =>
            x.PartyId == id &&
            x.MatterId == MatterPartyInput.MatterId &&
            x.Role == MatterPartyInput.Role);

        if (!exists)
        {
            db.MatterParties.Add(new MatterParty
            {
                PartyId = id,
                MatterId = MatterPartyInput.MatterId,
                Role = MatterPartyInput.Role,
                Notes = MatterPartyInput.Notes?.Trim() ?? string.Empty
            });
            await db.SaveChangesAsync();
        }

        return RedirectToPage(new { id });
    }

    private async Task LoadPageAsync(Guid id)
    {
        Party = await db.Parties
            .Include(x => x.Aliases)
            .Include(x => x.MatterParties)
                .ThenInclude(x => x.Matter)
                    .ThenInclude(x => x!.Client)
            .Include(x => x.OutboundRelationships)
                .ThenInclude(x => x.ToParty)
            .Include(x => x.InboundRelationships)
                .ThenInclude(x => x.FromParty)
            .FirstOrDefaultAsync(x => x.Id == id);

        PartyOptions = await db.Parties
            .Where(x => x.Id != id)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.PartyNumber:D8})", x.Id.ToString()))
            .ToListAsync();

        MatterOptions = await db.Matters
            .Include(x => x.Client)
            .OrderBy(x => x.MatterNumber)
            .Select(x => new SelectListItem($"{x.MatterNumber:D8} - {x.Name} / {x.Client!.Name}", x.Id.ToString()))
            .ToListAsync();
    }
}

public class AddAliasInput
{
    [Required]
    public string Alias { get; set; } = string.Empty;

    public string? Notes { get; set; }
}

public class AddRelationshipInput
{
    [Required]
    [Display(Name = "Related party")]
    public Guid ToPartyId { get; set; }

    [Display(Name = "Relationship")]
    public string RelationshipType { get; set; } = PartyRelationshipTypes.Related;

    public string? Notes { get; set; }
}

public class AddMatterPartyInput
{
    [Required]
    [Display(Name = "Matter")]
    public Guid MatterId { get; set; }

    public string Role { get; set; } = PartyRoles.Client;

    public string? Notes { get; set; }
}
