using System.ComponentModel.DataAnnotations;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Parties;

public class DetailsModel(
    CMIForgeDbContext db,
    EntityNoteService entityNoteService,
    CurrentUserService currentUserService,
    PermissionService permissionService,
    AuditLogService auditLogService) : PageModel
{
    public Party? Party { get; private set; }

    public List<EntityNote> Notes { get; private set; } = [];

    public List<AuditLog> AuditHistory { get; private set; } = [];

    public List<PartyRelatedContactRow> RelatedContacts { get; private set; } = [];

    public List<SelectListItem> PartyOptions { get; private set; } = [];

    public List<SelectListItem> MatterOptions { get; private set; } = [];

    public SelectList RelationshipTypeOptions { get; } = new(PartyRelationshipTypes.All);

    public SelectList PartyRoleOptions { get; } = new(PartyRoles.All);

    public bool CanEditEntities { get; private set; }

    [BindProperty]
    public AddAliasInput AliasInput { get; set; } = new();

    [BindProperty]
    public EditAliasInput AliasEditInput { get; set; } = new();

    [BindProperty]
    public AddRelationshipInput RelationshipInput { get; set; } = new();

    [BindProperty]
    public AddMatterPartyInput MatterPartyInput { get; set; } = new();

    [BindProperty]
    public string NewNote { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        await LoadPageAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostAliasAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        await LoadPageAsync(id);
        if (Party is null)
        {
            return NotFound();
        }

        ModelState.ClearValidationState(nameof(AliasEditInput));
        if (!TryValidateModel(AliasInput, nameof(AliasInput)))
        {
            return Page();
        }

        var normalizedAlias = ConflictSearchService.NormalizeName(AliasInput.Alias);
        var exists = await db.PartyAliases.AnyAsync(x => x.PartyId == id && x.NormalizedAlias == normalizedAlias);
        if (exists)
        {
            ModelState.AddModelError("AliasInput.Alias", "That alias already exists for this party.");
            return Page();
        }

        db.PartyAliases.Add(new PartyAlias
        {
            PartyId = id,
            Alias = AliasInput.Alias.Trim(),
            NormalizedAlias = normalizedAlias,
            Notes = AliasInput.Notes?.Trim() ?? string.Empty
        });
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Party.AliasAdded",
            "Party",
            id,
            Party.PartyNumber.ToString("D8"),
            $"Added alias {AliasInput.Alias.Trim()} to {Party.Name}.");

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUpdateAliasAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        await LoadPageAsync(id);
        if (Party is null)
        {
            return NotFound();
        }

        ModelState.ClearValidationState(nameof(AliasInput));
        if (!TryValidateModel(AliasEditInput, nameof(AliasEditInput)))
        {
            return Page();
        }

        var alias = await db.PartyAliases.FirstOrDefaultAsync(x => x.Id == AliasEditInput.Id && x.PartyId == id);
        if (alias is null)
        {
            return NotFound();
        }

        var normalizedAlias = ConflictSearchService.NormalizeName(AliasEditInput.Alias);
        var duplicate = await db.PartyAliases.AnyAsync(x =>
            x.PartyId == id &&
            x.Id != alias.Id &&
            x.NormalizedAlias == normalizedAlias);
        if (duplicate)
        {
            ModelState.AddModelError("AliasEditInput.Alias", "That alias already exists for this party.");
            return Page();
        }

        var oldAlias = alias.Alias;
        alias.Alias = AliasEditInput.Alias.Trim();
        alias.NormalizedAlias = normalizedAlias;
        alias.Notes = AliasEditInput.Notes?.Trim() ?? string.Empty;
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Party.AliasUpdated",
            "Party",
            id,
            Party.PartyNumber.ToString("D8"),
            $"Updated party alias {oldAlias} to {alias.Alias}.",
            new { oldAlias, alias.Alias, alias.Notes });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAliasAsync(Guid id, Guid aliasId)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        await LoadPageAsync(id);
        if (Party is null)
        {
            return NotFound();
        }

        var alias = await db.PartyAliases.FirstOrDefaultAsync(x => x.Id == aliasId && x.PartyId == id);
        if (alias is null)
        {
            return NotFound();
        }

        var aliasName = alias.Alias;
        db.PartyAliases.Remove(alias);
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Party.AliasDeleted",
            "Party",
            id,
            Party.PartyNumber.ToString("D8"),
            $"Deleted party alias {aliasName} from {Party.Name}.");

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRelationshipAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

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
            await auditLogService.LogAsync(
                "Party.RelationshipAdded",
                "Party",
                id,
                Party.PartyNumber.ToString("D8"),
                $"Added {RelationshipInput.RelationshipType} relationship for {Party.Name}.",
                new { RelationshipInput.ToPartyId });
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostMatterPartyAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

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
            await auditLogService.LogAsync(
                "Party.MatterRoleAdded",
                "Party",
                id,
                Party.PartyNumber.ToString("D8"),
                $"Linked {Party.Name} to matter role {MatterPartyInput.Role}.",
                new { MatterPartyInput.MatterId });
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostNoteAsync(Guid id)
    {
        var partyExists = await db.Parties.AnyAsync(x => x.Id == id);
        if (!partyExists)
        {
            return NotFound();
        }

        var impersonation = await currentUserService.GetImpersonationContextAsync();
        var actualUser = impersonation?.ActualUser ?? await currentUserService.GetActualCurrentUserAsync();
        await entityNoteService.AddAsync(EntityNoteService.PartyEntityType, id, NewNote, actualUser?.Id, impersonation?.ImpersonatedUser.Id);
        await auditLogService.LogAsync("Party.NoteAdded", "Party", id, null, "Added party discussion note.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteNoteAsync(Guid id, Guid noteId)
    {
        var partyExists = await db.Parties.AnyAsync(x => x.Id == id);
        if (!partyExists)
        {
            return NotFound();
        }

        var actualUser = await currentUserService.GetActualCurrentUserAsync();
        var canManageNotes = await permissionService.HasAsync(PermissionKeys.EntitiesEdit);
        var deleted = await entityNoteService.DeleteAsync(EntityNoteService.PartyEntityType, id, noteId, actualUser?.Id, canManageNotes);
        if (!deleted)
        {
            return Forbid();
        }

        await auditLogService.LogAsync("Party.NoteDeleted", "Party", id, null, "Deleted party discussion note.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostArchiveAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        var party = await db.Parties.FirstOrDefaultAsync(x => x.Id == id);
        if (party is null)
        {
            return NotFound();
        }

        party.IsArchived = !party.IsArchived;
        party.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            party.IsArchived ? "Party.Archived" : "Party.Restored",
            "Party",
            party.Id,
            party.PartyNumber.ToString("D8"),
            $"{(party.IsArchived ? "Archived" : "Restored")} party {party.Name}.");

        return RedirectToPage(new { id });
    }

    private async Task LoadPageAsync(Guid id)
    {
        CanEditEntities = await permissionService.HasAsync(PermissionKeys.EntitiesEdit);
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

        if (Party is not null)
        {
            var matterIds = Party.MatterParties.Select(x => x.MatterId).Distinct().ToList();
            RelatedContacts = matterIds.Count == 0
                ? []
                : await db.MatterContacts
                    .Include(x => x.Contact)
                    .Include(x => x.Matter)
                        .ThenInclude(x => x!.Client)
                    .Where(x => matterIds.Contains(x.MatterId))
                    .OrderBy(x => x.Matter!.MatterNumber)
                    .ThenByDescending(x => x.IsPrimary)
                    .ThenBy(x => x.Contact!.DisplayName)
                    .Select(x => new PartyRelatedContactRow(
                        x.ContactId,
                        x.Contact!.ContactNumber,
                        x.Contact.DisplayName,
                        x.Contact.Email,
                        x.MatterId,
                        x.Matter!.MatterNumber,
                        x.Matter.Name,
                        x.Matter.Client!.Name,
                        x.Role,
                        x.IsPrimary))
                    .ToListAsync();
        }

        PartyOptions = await db.Parties
            .Where(x => x.Id != id)
            .Where(x => !x.IsArchived)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.PartyNumber:D8})", x.Id.ToString()))
            .ToListAsync();

        MatterOptions = await db.Matters
            .Include(x => x.Client)
            .Where(x => !x.IsArchived)
            .OrderBy(x => x.MatterNumber)
            .Select(x => new SelectListItem($"{x.MatterNumber:D8} - {x.Name} / {x.Client!.Name}", x.Id.ToString()))
            .ToListAsync();

        Notes = Party is null
            ? []
            : await entityNoteService.ListAsync(EntityNoteService.PartyEntityType, id);

        AuditHistory = Party is null
            ? []
            : await auditLogService.ListForEntityAsync("Party", id);
    }
}

public class AddAliasInput
{
    [Required]
    [StringLength(240)]
    public string Alias { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }
}

public class EditAliasInput : AddAliasInput
{
    [Required]
    public Guid Id { get; set; }
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

public record PartyRelatedContactRow(
    Guid ContactId,
    int ContactNumber,
    string ContactName,
    string Email,
    Guid MatterId,
    int MatterNumber,
    string MatterName,
    string ClientName,
    string Role,
    bool IsPrimary);
