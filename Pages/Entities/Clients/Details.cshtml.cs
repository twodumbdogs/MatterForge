using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Clients;

public class DetailsModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    EntityNoteService entityNoteService,
    CurrentUserService currentUserService,
    AuditLogService auditLogService,
    ExternalFormInviteTrackingService inviteTrackingService) : PageModel
{
    public Client? Client { get; private set; }

    public List<EntityNote> Notes { get; private set; } = [];

    public List<AuditLog> AuditHistory { get; private set; } = [];

    public List<EntityChangeRequest> PendingChanges { get; private set; } = [];

    public List<ClientRelatedPartyRow> RelatedParties { get; private set; } = [];

    public List<ExternalFormInviteTrackingRow> RecentInviteSends { get; private set; } = [];

    public bool CanEditEntities { get; private set; }

    [BindProperty]
    public string NewNote { get; set; } = string.Empty;

    [BindProperty]
    public ClientAliasInput AliasInput { get; set; } = new();

    [BindProperty]
    public ClientAliasEditInput AliasEditInput { get; set; } = new();

    public async Task OnGetAsync(Guid id)
    {
        await LoadPageAsync(id);
    }

    public async Task<IActionResult> OnPostAliasAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        await LoadPageAsync(id);
        if (Client is null)
        {
            return NotFound();
        }

        ModelState.ClearValidationState(nameof(AliasEditInput));
        if (!TryValidateModel(AliasInput, nameof(AliasInput)))
        {
            return Page();
        }

        var normalizedAlias = ConflictSearchService.NormalizeName(AliasInput.Alias);
        var exists = await db.ClientAliases.AnyAsync(x => x.ClientId == id && x.NormalizedAlias == normalizedAlias);
        if (exists)
        {
            ModelState.AddModelError("AliasInput.Alias", "That alias already exists for this client.");
            return Page();
        }

        db.ClientAliases.Add(new ClientAlias
        {
            ClientId = id,
            Alias = AliasInput.Alias.Trim(),
            NormalizedAlias = normalizedAlias,
            Notes = AliasInput.Notes?.Trim() ?? string.Empty
        });
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Client.AliasAdded",
            "Client",
            id,
            Client.ClientNumber.ToString("D8"),
            $"Added alias {AliasInput.Alias.Trim()} to {Client.Name}.");

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUpdateAliasAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        await LoadPageAsync(id);
        if (Client is null)
        {
            return NotFound();
        }

        ModelState.ClearValidationState(nameof(AliasInput));
        if (!TryValidateModel(AliasEditInput, nameof(AliasEditInput)))
        {
            return Page();
        }

        var alias = await db.ClientAliases.FirstOrDefaultAsync(x => x.Id == AliasEditInput.Id && x.ClientId == id);
        if (alias is null)
        {
            return NotFound();
        }

        var normalizedAlias = ConflictSearchService.NormalizeName(AliasEditInput.Alias);
        var duplicate = await db.ClientAliases.AnyAsync(x =>
            x.ClientId == id &&
            x.Id != alias.Id &&
            x.NormalizedAlias == normalizedAlias);
        if (duplicate)
        {
            ModelState.AddModelError("AliasEditInput.Alias", "That alias already exists for this client.");
            return Page();
        }

        var oldAlias = alias.Alias;
        alias.Alias = AliasEditInput.Alias.Trim();
        alias.NormalizedAlias = normalizedAlias;
        alias.Notes = AliasEditInput.Notes?.Trim() ?? string.Empty;
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Client.AliasUpdated",
            "Client",
            id,
            Client.ClientNumber.ToString("D8"),
            $"Updated client alias {oldAlias} to {alias.Alias}.",
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
        if (Client is null)
        {
            return NotFound();
        }

        var alias = await db.ClientAliases.FirstOrDefaultAsync(x => x.Id == aliasId && x.ClientId == id);
        if (alias is null)
        {
            return NotFound();
        }

        var aliasName = alias.Alias;
        db.ClientAliases.Remove(alias);
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Client.AliasDeleted",
            "Client",
            id,
            Client.ClientNumber.ToString("D8"),
            $"Deleted client alias {aliasName} from {Client.Name}.");

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostNoteAsync(Guid id)
    {
        var clientExists = await db.Clients.AnyAsync(x => x.Id == id);
        if (!clientExists)
        {
            return NotFound();
        }

        var impersonation = await currentUserService.GetImpersonationContextAsync();
        var actualUser = impersonation?.ActualUser ?? await currentUserService.GetActualCurrentUserAsync();
        await entityNoteService.AddAsync(EntityNoteService.ClientEntityType, id, NewNote, actualUser?.Id, impersonation?.ImpersonatedUser.Id);
        await auditLogService.LogAsync("Client.NoteAdded", "Client", id, null, "Added client discussion note.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteNoteAsync(Guid id, Guid noteId)
    {
        var clientExists = await db.Clients.AnyAsync(x => x.Id == id);
        if (!clientExists)
        {
            return NotFound();
        }

        var actualUser = await currentUserService.GetActualCurrentUserAsync();
        var canManageNotes = await permissionService.HasAsync(PermissionKeys.EntitiesEdit);
        var deleted = await entityNoteService.DeleteAsync(EntityNoteService.ClientEntityType, id, noteId, actualUser?.Id, canManageNotes);
        if (!deleted)
        {
            return Forbid();
        }

        await auditLogService.LogAsync("Client.NoteDeleted", "Client", id, null, "Deleted client discussion note.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostArchiveAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesEdit))
        {
            return Forbid();
        }

        var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == id);
        if (client is null)
        {
            return NotFound();
        }

        client.IsArchived = !client.IsArchived;
        client.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            client.IsArchived ? "Client.Archived" : "Client.Restored",
            "Client",
            client.Id,
            client.ClientNumber.ToString("D8"),
            $"{(client.IsArchived ? "Archived" : "Restored")} client {client.Name}.");

        return RedirectToPage(new { id });
    }

    private async Task LoadPageAsync(Guid id)
    {
        CanEditEntities = await permissionService.HasAsync(PermissionKeys.EntitiesEdit);
        Client = await db.Clients
            .Include(x => x.Aliases)
            .Include(x => x.Matters)
                .ThenInclude(x => x.ResponsibleUser)
            .Include(x => x.Contacts)
                .ThenInclude(x => x.Contact)
            .FirstOrDefaultAsync(x => x.Id == id);

        RelatedParties = await db.MatterParties
            .Include(x => x.Matter)
            .Include(x => x.Party)
            .Where(x => x.Matter != null && x.Matter.ClientId == id)
            .OrderBy(x => x.Matter!.MatterNumber)
            .ThenBy(x => x.Role)
            .ThenBy(x => x.Party!.Name)
            .Select(x => new ClientRelatedPartyRow(
                x.PartyId,
                x.Party!.PartyNumber,
                x.Party.Name,
                x.MatterId,
                x.Matter!.MatterNumber,
                x.Matter.Name,
                x.Role))
            .ToListAsync();

        PendingChanges = await db.EntityChangeRequests
            .Include(x => x.RequestedByUser)
            .Where(x => x.EntityType == EntityChangeService.ClientEntityType &&
                x.EntityId == id &&
                x.Status == EntityChangeRequestStatuses.Pending)
            .OrderBy(x => x.RequestedAt)
            .ToListAsync();

        Notes = Client is null
            ? []
            : await entityNoteService.ListAsync(EntityNoteService.ClientEntityType, id);

        AuditHistory = Client is null
            ? []
            : await auditLogService.ListForEntityAsync("Client", id);

        RecentInviteSends = Client is null
            ? []
            : await inviteTrackingService.ListForClientAsync(id);
    }
}

public class ClientAliasInput
{
    [Required]
    [StringLength(240)]
    public string Alias { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }
}

public class ClientAliasEditInput : ClientAliasInput
{
    [Required]
    public Guid Id { get; set; }
}

public record ClientRelatedPartyRow(
    Guid PartyId,
    int PartyNumber,
    string PartyName,
    Guid MatterId,
    int MatterNumber,
    string MatterName,
    string Role);
