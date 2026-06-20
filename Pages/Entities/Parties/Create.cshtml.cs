using System.ComponentModel.DataAnnotations;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Parties;

public class CreateModel(CMIForgeDbContext db, AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public PartyInput Input { get; set; } = new();

    public SelectList PartyTypeOptions { get; } = new(PartyTypes.All);

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var normalizedName = ConflictSearchService.NormalizeName(Input.Name);
        var existingParty = await db.Parties.FirstOrDefaultAsync(x => x.NormalizedName == normalizedName);
        if (existingParty is not null)
        {
            ModelState.AddModelError("Input.Name", $"A party already exists with normalized name {normalizedName}.");
            return Page();
        }

        var nextPartyNumber = (await db.Parties.MaxAsync(x => (int?)x.PartyNumber) ?? 0) + 1;
        var party = new Party
        {
            PartyNumber = nextPartyNumber,
            Name = Input.Name.Trim(),
            NormalizedName = normalizedName,
            PartyType = Input.PartyType,
            Status = Input.Status.Trim(),
            Notes = Input.Notes?.Trim() ?? string.Empty
        };

        foreach (var alias in SplitAliases(Input.Aliases))
        {
            party.Aliases.Add(new PartyAlias
            {
                Alias = alias,
                NormalizedAlias = ConflictSearchService.NormalizeName(alias)
            });
        }

        db.Parties.Add(party);
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Party.Created",
            "Party",
            party.Id,
            party.PartyNumber.ToString("D8"),
            $"Created party {party.Name}.",
            new { party.PartyType, party.Status, AliasCount = party.Aliases.Count });

        return RedirectToPage("./Details", new { id = party.Id });
    }

    private static List<string> SplitAliases(string? aliases)
    {
        return (aliases ?? string.Empty)
            .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public class PartyInput
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Party type")]
    public string PartyType { get; set; } = PartyTypes.Organization;

    public string Status { get; set; } = "Active";

    public string? Aliases { get; set; }

    public string? Notes { get; set; }
}

