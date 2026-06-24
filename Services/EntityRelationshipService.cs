using System.ComponentModel.DataAnnotations;
using CMIForge.Data;
using CMIForge.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public class EntityRelationshipService(CMIForgeDbContext db)
{
    public async Task<List<SelectListItem>> GetRelationshipTypeOptionsAsync(params string[] scopes)
    {
        var allowedScopes = scopes.Length == 0 ? RelationshipScopes.All : scopes;
        return await db.RelationshipTypes
            .Where(x => x.IsActive && allowedScopes.Contains(x.Scope))
            .OrderBy(x => x.Scope)
            .ThenBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.Scope.Replace('_', '/')})", x.Id.ToString()))
            .ToListAsync();
    }

    public List<SelectListItem> GetTargetEntityTypeOptions(string fromEntityType)
    {
        return EntityRelationshipEntityTypes.All
            .Select(x => new SelectListItem(x, x))
            .ToList();
    }

    public async Task<List<EntityRelationshipDisplayRow>> ListForEntityAsync(string entityType, Guid entityId)
    {
        var relationships = await db.EntityRelationships
            .Include(x => x.RelationshipType)
            .Where(x =>
                (x.FromEntityType == entityType && x.FromEntityId == entityId) ||
                (x.ToEntityType == entityType && x.ToEntityId == entityId))
            .OrderBy(x => x.RelationshipType!.Name)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync();

        var rows = new List<EntityRelationshipDisplayRow>();
        foreach (var relationship in relationships)
        {
            var isOutbound = relationship.FromEntityType == entityType && relationship.FromEntityId == entityId;
            var otherType = isOutbound ? relationship.ToEntityType : relationship.FromEntityType;
            var otherId = isOutbound ? relationship.ToEntityId : relationship.FromEntityId;
            var other = await GetEntitySummaryAsync(otherType, otherId);

            rows.Add(new EntityRelationshipDisplayRow(
                relationship.Id,
                isOutbound ? "To" : "From",
                otherType,
                otherId,
                other?.Label ?? $"{otherType} {otherId}",
                GetDetailsUrl(otherType, otherId),
                relationship.RelationshipType?.Name ?? "Relationship",
                relationship.RelationshipType?.Scope ?? string.Empty,
                relationship.Notes,
                relationship.CreatedAt));
        }

        return rows;
    }

    public async Task AddRelationshipAsync(string fromEntityType, Guid fromEntityId, EntityRelationshipInput input)
    {
        if (string.IsNullOrWhiteSpace(input.TargetEntityType))
        {
            throw new InvalidOperationException("Choose a target entity type.");
        }

        if (!EntityRelationshipEntityTypes.All.Contains(input.TargetEntityType))
        {
            throw new InvalidOperationException("Choose a valid target entity type.");
        }

        var target = await ResolveEntityAsync(input.TargetEntityType, input.TargetRecord);
        if (target is null)
        {
            throw new InvalidOperationException("No matching target record was found. Use its record number or GUID.");
        }

        if (string.Equals(fromEntityType, input.TargetEntityType, StringComparison.OrdinalIgnoreCase) &&
            fromEntityId == target.Id)
        {
            throw new InvalidOperationException("A record cannot be related to itself.");
        }

        var relationshipType = await db.RelationshipTypes.FirstOrDefaultAsync(x => x.Id == input.RelationshipTypeId && x.IsActive);
        if (relationshipType is null)
        {
            throw new InvalidOperationException("Choose a relationship type.");
        }

        var expectedScope = GetScope(fromEntityType, input.TargetEntityType);
        if (!string.Equals(relationshipType.Scope, expectedScope, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The selected relationship type is for {relationshipType.Scope}, but this relationship needs {expectedScope}.");
        }

        var exists = await db.EntityRelationships.AnyAsync(x =>
            x.FromEntityType == fromEntityType &&
            x.FromEntityId == fromEntityId &&
            x.ToEntityType == input.TargetEntityType &&
            x.ToEntityId == target.Id &&
            x.RelationshipTypeId == relationshipType.Id);
        if (exists)
        {
            throw new InvalidOperationException("That relationship already exists.");
        }

        db.EntityRelationships.Add(new EntityRelationship
        {
            FromEntityType = fromEntityType,
            FromEntityId = fromEntityId,
            ToEntityType = input.TargetEntityType,
            ToEntityId = target.Id,
            RelationshipTypeId = relationshipType.Id,
            Notes = input.Notes?.Trim() ?? string.Empty
        });

        await db.SaveChangesAsync();
    }

    public async Task<bool> DeleteRelationshipAsync(string entityType, Guid entityId, Guid relationshipId)
    {
        var relationship = await db.EntityRelationships.FirstOrDefaultAsync(x =>
            x.Id == relationshipId &&
            ((x.FromEntityType == entityType && x.FromEntityId == entityId) ||
             (x.ToEntityType == entityType && x.ToEntityId == entityId)));
        if (relationship is null)
        {
            return false;
        }

        db.EntityRelationships.Remove(relationship);
        await db.SaveChangesAsync();
        return true;
    }

    private async Task<EntitySummary?> ResolveEntityAsync(string entityType, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (Guid.TryParse(trimmed, out var id))
        {
            return await GetEntitySummaryAsync(entityType, id);
        }

        var numberText = new string(trimmed.Where(char.IsDigit).ToArray());
        if (!int.TryParse(numberText, out var number))
        {
            return null;
        }

        return entityType switch
        {
            EntityRelationshipEntityTypes.Client => await db.Clients
                .Where(x => !x.IsArchived && x.ClientNumber == number)
                .Select(x => new EntitySummary(x.Id, $"{x.ClientNumber:D8} - {x.Name}"))
                .FirstOrDefaultAsync(),
            EntityRelationshipEntityTypes.Matter => await db.Matters
                .Where(x => !x.IsArchived && x.MatterNumber == number)
                .Select(x => new EntitySummary(x.Id, $"{x.MatterNumber:D8} - {x.Name}"))
                .FirstOrDefaultAsync(),
            EntityRelationshipEntityTypes.Party => await db.Parties
                .Where(x => !x.IsArchived && x.PartyNumber == number)
                .Select(x => new EntitySummary(x.Id, $"{x.PartyNumber:D8} - {x.Name}"))
                .FirstOrDefaultAsync(),
            EntityRelationshipEntityTypes.Contact => await db.Contacts
                .Where(x => !x.IsArchived && x.ContactNumber == number)
                .Select(x => new EntitySummary(x.Id, $"{x.ContactNumber:D8} - {x.DisplayName}"))
                .FirstOrDefaultAsync(),
            EntityRelationshipEntityTypes.User => await db.Users
                .Where(x => !x.IsArchived && x.SystemId == number)
                .Select(x => new EntitySummary(x.Id, $"{x.SystemId:D8} - {x.DisplayName}"))
                .FirstOrDefaultAsync(),
            _ => null
        };
    }

    private async Task<EntitySummary?> GetEntitySummaryAsync(string entityType, Guid id)
    {
        return entityType switch
        {
            EntityRelationshipEntityTypes.Client => await db.Clients
                .Where(x => x.Id == id)
                .Select(x => new EntitySummary(x.Id, $"{x.ClientNumber:D8} - {x.Name}"))
                .FirstOrDefaultAsync(),
            EntityRelationshipEntityTypes.Matter => await db.Matters
                .Where(x => x.Id == id)
                .Select(x => new EntitySummary(x.Id, $"{x.MatterNumber:D8} - {x.Name}"))
                .FirstOrDefaultAsync(),
            EntityRelationshipEntityTypes.Party => await db.Parties
                .Where(x => x.Id == id)
                .Select(x => new EntitySummary(x.Id, $"{x.PartyNumber:D8} - {x.Name}"))
                .FirstOrDefaultAsync(),
            EntityRelationshipEntityTypes.Contact => await db.Contacts
                .Where(x => x.Id == id)
                .Select(x => new EntitySummary(x.Id, $"{x.ContactNumber:D8} - {x.DisplayName}"))
                .FirstOrDefaultAsync(),
            EntityRelationshipEntityTypes.User => await db.Users
                .Where(x => x.Id == id)
                .Select(x => new EntitySummary(x.Id, $"{x.SystemId:D8} - {x.DisplayName}"))
                .FirstOrDefaultAsync(),
            _ => null
        };
    }

    private static string GetScope(string fromEntityType, string toEntityType)
    {
        var fromUser = EntityRelationshipEntityTypes.IsUser(fromEntityType);
        var toUser = EntityRelationshipEntityTypes.IsUser(toEntityType);
        if (fromUser && toUser)
        {
            return RelationshipScopes.UserUser;
        }

        return fromUser || toUser ? RelationshipScopes.EntityUser : RelationshipScopes.EntityEntity;
    }

    private static string GetDetailsUrl(string entityType, Guid entityId)
    {
        return entityType switch
        {
            EntityRelationshipEntityTypes.Client => $"/Entities/Clients/Details/{entityId}",
            EntityRelationshipEntityTypes.Matter => $"/Entities/Matters/Details/{entityId}",
            EntityRelationshipEntityTypes.Party => $"/Entities/Parties/Details/{entityId}",
            EntityRelationshipEntityTypes.Contact => $"/Entities/Contacts/Details/{entityId}",
            EntityRelationshipEntityTypes.User => $"/Entities/Users/Details/{entityId}",
            _ => "#"
        };
    }

    private record EntitySummary(Guid Id, string Label);
}

public class EntityRelationshipInput
{
    [Required]
    public string TargetEntityType { get; set; } = EntityRelationshipEntityTypes.Party;

    [Required]
    [StringLength(80)]
    public string TargetRecord { get; set; } = string.Empty;

    [Required]
    public Guid RelationshipTypeId { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}

public record EntityRelationshipDisplayRow(
    Guid Id,
    string Direction,
    string OtherEntityType,
    Guid OtherEntityId,
    string OtherLabel,
    string OtherUrl,
    string RelationshipType,
    string RelationshipScope,
    string Notes,
    DateTimeOffset CreatedAt);

public record EntityRelationshipsPanelModel(
    string Title,
    Guid CurrentEntityId,
    bool CanManage,
    EntityRelationshipInput Input,
    IReadOnlyList<SelectListItem> TargetEntityTypeOptions,
    IReadOnlyList<SelectListItem> RelationshipTypeOptions,
    IReadOnlyList<EntityRelationshipDisplayRow> Rows);
