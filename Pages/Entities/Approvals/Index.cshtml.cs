using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CMIForge.Pages.Entities.Approvals;

public class IndexModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    CurrentUserService currentUserService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public string? ReviewNotes { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public List<EntityChangeRequest> PendingRequests { get; private set; } = [];

    public List<EntityChangeRequest> RecentReviewedRequests { get; private set; } = [];

    public Dictionary<Guid, List<EntityChangeDiffRow>> ChangeDiffs { get; private set; } = [];

    public RecordPage PendingPagination { get; private set; } = RecordPage.Empty;

    public bool CanApprove { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesView))
        {
            return Forbid();
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesApprove))
        {
            return Forbid();
        }

        var request = await db.EntityChangeRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (request is null || request.Status != EntityChangeRequestStatuses.Pending)
        {
            return RedirectToPage();
        }

        var actor = await currentUserService.GetCurrentUserAsync();
        var complianceReviewCompleted = false;
        if (request.EntityType == EntityChangeService.ClientEntityType)
        {
            var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == request.EntityId);
            var proposed = EntityChangeService.Deserialize<ClientChangeSnapshot>(request.ProposedValuesJson);
            if (client is null || proposed is null)
            {
                return RedirectToPage();
            }

            complianceReviewCompleted = EntityComplianceService.MovesFromReviewToCompliant(
                request.EntityType,
                client.Status,
                proposed.Status);
            EntityChangeService.Apply(client, proposed);
        }
        else if (request.EntityType == EntityChangeService.MatterEntityType)
        {
            var matter = await db.Matters.FirstOrDefaultAsync(x => x.Id == request.EntityId);
            var proposed = EntityChangeService.Deserialize<MatterChangeSnapshot>(request.ProposedValuesJson);
            if (matter is null || proposed is null)
            {
                return RedirectToPage();
            }

            complianceReviewCompleted = EntityComplianceService.MovesFromReviewToCompliant(
                request.EntityType,
                matter.Status,
                proposed.Status);
            EntityChangeService.Apply(matter, proposed);
        }
        else if (request.EntityType == EntityChangeService.ContactEntityType)
        {
            var contact = await db.Contacts.FirstOrDefaultAsync(x => x.Id == request.EntityId);
            var proposed = EntityChangeService.Deserialize<ContactChangeSnapshot>(request.ProposedValuesJson);
            if (contact is null || proposed is null)
            {
                return RedirectToPage();
            }

            EntityChangeService.Apply(contact, proposed);
        }

        request.Status = EntityChangeRequestStatuses.Approved;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedByUserId = actor?.Id;
        request.ReviewNotes = ReviewNotes?.Trim() ?? string.Empty;

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "EntityChangeApproved",
            request.EntityType,
            request.EntityId,
            request.EntityNumber,
            $"Approved {request.EntityType.ToLowerInvariant()} change request for {request.EntityName}.",
            new { request.Id, request.Summary, request.ReviewNotes });

        if (complianceReviewCompleted)
        {
            await auditLogService.LogAsync(
                "EntityComplianceReviewed",
                request.EntityType,
                request.EntityId,
                request.EntityNumber,
                $"Marked {request.EntityType.ToLowerInvariant()} {request.EntityName} as compliance reviewed.",
                new
                {
                    request.Id,
                    request.Summary,
                    request.RequestNotes,
                    request.ReviewNotes,
                    ReviewedByUserId = actor?.Id
                });
        }

        return RedirectToPage(new { pageNumber = PageNumber, Search });
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.EntitiesApprove))
        {
            return Forbid();
        }

        var request = await db.EntityChangeRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (request is null || request.Status != EntityChangeRequestStatuses.Pending)
        {
            return RedirectToPage();
        }

        var actor = await currentUserService.GetCurrentUserAsync();
        request.Status = EntityChangeRequestStatuses.Rejected;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedByUserId = actor?.Id;
        request.ReviewNotes = ReviewNotes?.Trim() ?? string.Empty;

        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "EntityChangeRejected",
            request.EntityType,
            request.EntityId,
            request.EntityNumber,
            $"Rejected {request.EntityType.ToLowerInvariant()} change request for {request.EntityName}.",
            new { request.Id, request.Summary, request.ReviewNotes });

        return RedirectToPage(new { pageNumber = PageNumber, Search });
    }

    private async Task LoadAsync()
    {
        CanApprove = await permissionService.HasAsync(PermissionKeys.EntitiesApprove);

        var pendingQuery = db.EntityChangeRequests
            .Where(x => x.Status == EntityChangeRequestStatuses.Pending);

        Search = Search?.Trim();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            pendingQuery = pendingQuery.Where(x =>
                x.EntityType.Contains(Search) ||
                x.EntityNumber.Contains(Search) ||
                x.EntityName.Contains(Search) ||
                x.Summary.Contains(Search) ||
                x.RequestNotes.Contains(Search) ||
                (x.RequestedByUser != null && x.RequestedByUser.DisplayName.Contains(Search)));
        }

        PendingPagination = RecordPage.Create(PageNumber, await pendingQuery.CountAsync());
        PageNumber = PendingPagination.PageNumber;

        PendingRequests = await pendingQuery
            .Include(x => x.RequestedByUser)
            .OrderBy(x => x.RequestedAt)
            .Skip(PendingPagination.Skip)
            .Take(PendingPagination.PageSize)
            .ToListAsync();

        var recentReviewedQuery = db.EntityChangeRequests
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewedByUser)
            .Where(x => x.Status != EntityChangeRequestStatuses.Pending);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            recentReviewedQuery = recentReviewedQuery.Where(x =>
                x.Status.Contains(Search) ||
                x.EntityType.Contains(Search) ||
                x.EntityNumber.Contains(Search) ||
                x.EntityName.Contains(Search) ||
                x.Summary.Contains(Search) ||
                x.RequestNotes.Contains(Search) ||
                x.ReviewNotes.Contains(Search) ||
                (x.RequestedByUser != null && x.RequestedByUser.DisplayName.Contains(Search)) ||
                (x.ReviewedByUser != null && x.ReviewedByUser.DisplayName.Contains(Search)));
        }

        RecentReviewedRequests = await recentReviewedQuery
            .OrderByDescending(x => x.ReviewedAt)
            .Take(20)
            .ToListAsync();

        await BuildChangeDiffsAsync(PendingRequests.Concat(RecentReviewedRequests));
    }

    private async Task BuildChangeDiffsAsync(IEnumerable<EntityChangeRequest> requests)
    {
        var requestList = requests.ToList();
        var ids = requestList
            .Where(x => x.EntityType == EntityChangeService.MatterEntityType)
            .SelectMany(GetMatterReferenceIds)
            .ToHashSet();

        var clientNames = await db.Clients
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name);
        var userNames = await db.Users
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName);
        var timeCodeSetNames = await db.TimeCodeSets
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        ChangeDiffs = requestList.ToDictionary(
            x => x.Id,
            x => BuildDiffRows(x, clientNames, userNames, timeCodeSetNames));
    }

    private static IEnumerable<Guid> GetMatterReferenceIds(EntityChangeRequest request)
    {
        foreach (var values in new[] { ReadSnapshot(request.CurrentValuesJson), ReadSnapshot(request.ProposedValuesJson) })
        {
            foreach (var field in new[] { "clientId", "responsibleUserId", "leadPartnerId", "timeCodeSetId" })
            {
                if (values.TryGetValue(field, out var value) && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id))
                {
                    yield return id;
                }
            }
        }
    }

    private static List<EntityChangeDiffRow> BuildDiffRows(
        EntityChangeRequest request,
        Dictionary<Guid, string> clientNames,
        Dictionary<Guid, string> userNames,
        Dictionary<Guid, string> timeCodeSetNames)
    {
        var currentValues = ReadSnapshot(request.CurrentValuesJson);
        var proposedValues = ReadSnapshot(request.ProposedValuesJson);
        return currentValues.Keys
            .Union(proposedValues.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(FieldSort)
            .Select(field =>
            {
                currentValues.TryGetValue(field, out var currentValue);
                proposedValues.TryGetValue(field, out var proposedValue);
                return new EntityChangeDiffRow(
                    LabelForField(field),
                    FormatSnapshotValue(field, currentValue, clientNames, userNames, timeCodeSetNames),
                    FormatSnapshotValue(field, proposedValue, clientNames, userNames, timeCodeSetNames));
            })
            .Where(x => !string.Equals(x.CurrentValue, x.ProposedValue, StringComparison.Ordinal))
            .ToList();
    }

    private static Dictionary<string, JsonElement> ReadSnapshot(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, FormJson.Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string FormatSnapshotValue(
        string field,
        JsonElement value,
        Dictionary<Guid, string> clientNames,
        Dictionary<Guid, string> userNames,
        Dictionary<Guid, string> timeCodeSetNames)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return "(blank)";
        }

        if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
        {
            return value.GetBoolean() ? "Yes" : "No";
        }

        var text = value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return "(blank)";
        }

        if (Guid.TryParse(text, out var id))
        {
            return field switch
            {
                "clientId" when clientNames.TryGetValue(id, out var name) => name,
                "responsibleUserId" or "leadPartnerId" when userNames.TryGetValue(id, out var name) => name,
                "timeCodeSetId" when timeCodeSetNames.TryGetValue(id, out var name) => name,
                _ => text
            };
        }

        return text;
    }

    private static int FieldSort(string field)
    {
        return field switch
        {
            "name" or "firstName" or "lastName" => 0,
            "clientId" => 1,
            "status" => 2,
            _ => 10
        };
    }

    private static string LabelForField(string field)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["clientId"] = "Client",
            ["responsibleUserId"] = "Responsible user",
            ["leadPartnerId"] = "Lead partner",
            ["requiresTimeApproval"] = "Requires time approval",
            ["timeIncrementMinutes"] = "Time increment",
            ["timeCodeSetId"] = "Time code set",
            ["addressLine1"] = "Address line 1",
            ["addressLine2"] = "Address line 2",
            ["postalCode"] = "Postal code",
            ["mobilePhone"] = "Mobile phone",
            ["primaryContact"] = "Primary contact",
            ["practiceArea"] = "Practice area",
            ["openedDate"] = "Opened date"
        };

        if (labels.TryGetValue(field, out var label))
        {
            return label;
        }

        var title = field.Length == 0 ? field : char.ToUpperInvariant(field[0]) + field[1..];
        var words = new List<char>();
        foreach (var character in title)
        {
            if (char.IsUpper(character) && words.Count > 0)
            {
                words.Add(' ');
            }

            words.Add(character);
        }

        return new string(words.ToArray());
    }
}

public sealed record EntityChangeDiffRow(string Field, string CurrentValue, string ProposedValue);
