using System.IO.Compression;
using System.Text.Json;
using CMIForge.Data;
using CMIForge.Models;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public class ConflictSearchArchiveService(CMIForgeDbContext db)
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task<int> ArchiveExistingClearedSearchesAsync(int batchSize = 100)
    {
        var totalArchived = 0;
        while (true)
        {
            var clearedSearchIds = await db.ConflictSearches
                .Where(x =>
                    x.ArchivedAt == null &&
                    x.Status == ConflictSearchStatuses.Cleared &&
                    x.ReviewerDecision == ConflictSearchDecisions.Clear)
                .OrderBy(x => x.SearchNumber)
                .Select(x => x.Id)
                .Take(batchSize)
                .ToListAsync();

            if (clearedSearchIds.Count == 0)
            {
                return totalArchived;
            }

            foreach (var searchId in clearedSearchIds)
            {
                await ArchiveIfClearedAsync(searchId);
            }

            await db.SaveChangesAsync();
            totalArchived += clearedSearchIds.Count;
            db.ChangeTracker.Clear();
        }
    }

    public async Task ArchiveIfClearedAsync(Guid searchId)
    {
        var search = await db.ConflictSearches
            .Include(x => x.FormSubmission)
                .ThenInclude(x => x!.FormDefinition)
            .Include(x => x.Matter)
                .ThenInclude(x => x!.Client)
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewedByUser)
            .Include(x => x.Results)
                .ThenInclude(x => x.Party)
            .Include(x => x.Results)
                .ThenInclude(x => x.Matter)
                    .ThenInclude(x => x!.Client)
            .Include(x => x.Results)
                .ThenInclude(x => x.Client)
            .Include(x => x.Results)
                .ThenInclude(x => x.ClearedByUser)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == searchId);
        if (search is null || search.ReviewerDecision != ConflictSearchDecisions.Clear || search.Status != ConflictSearchStatuses.Cleared)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var payload = BuildPayload(search);
        var searchableText = BuildSearchableText(payload);
        var normalizedSearchableText = ConflictSearchService.NormalizeName(searchableText);
        var payloadBytes = Compress(JsonSerializer.SerializeToUtf8Bytes(payload, PayloadJsonOptions));
        var highestRisk = payload.Results
            .OrderByDescending(x => Array.IndexOf(ConflictRiskLevels.All, x.RiskLevel))
            .ThenByDescending(x => x.Score)
            .FirstOrDefault()?.RiskLevel ?? ConflictRiskLevels.Low;

        var archive = await db.ConflictSearchArchives
            .Include(x => x.Hits)
            .FirstOrDefaultAsync(x => x.ConflictSearchId == search.Id);
        if (archive is null)
        {
            archive = new ConflictSearchArchive
            {
                ConflictSearchId = search.Id
            };
            db.ConflictSearchArchives.Add(archive);
        }
        else
        {
            db.ConflictSearchHitArchives.RemoveRange(archive.Hits);
            archive.Hits.Clear();
        }

        archive.SearchNumber = search.SearchNumber;
        archive.SearchName = search.SearchName;
        archive.SearchTerms = search.SearchTerms;
        archive.NormalizedTerms = search.NormalizedTerms;
        archive.SearchableText = searchableText;
        archive.NormalizedSearchableText = normalizedSearchableText;
        archive.FormSubmissionId = search.FormSubmissionId;
        archive.SubmissionNumber = search.FormSubmission?.SubmissionNumber;
        archive.FormName = search.FormSubmission?.FormDefinition?.Name ?? string.Empty;
        archive.MatterId = search.MatterId;
        archive.MatterNumber = search.Matter?.MatterNumber;
        archive.MatterName = search.Matter?.Name ?? string.Empty;
        archive.ClientId = search.Matter?.ClientId;
        archive.ClientNumber = search.Matter?.Client?.ClientNumber;
        archive.ClientName = search.Matter?.Client?.Name ?? string.Empty;
        archive.RequestedByUserId = search.RequestedByUserId;
        archive.RequestedByDisplayName = search.RequestedByUser?.DisplayName ?? "System";
        archive.ReviewedByUserId = search.ReviewedByUserId;
        archive.ReviewedByDisplayName = search.ReviewedByUser?.DisplayName ?? string.Empty;
        archive.Status = search.Status;
        archive.ReviewerDecision = search.ReviewerDecision;
        archive.ReviewNotes = search.ReviewNotes;
        archive.AiSummary = search.AiSummary;
        archive.ResultCount = payload.Results.Count;
        archive.HighestRiskLevel = highestRisk;
        archive.CreatedAt = search.CreatedAt;
        archive.UpdatedAt = search.UpdatedAt;
        archive.ReviewedAt = search.ReviewedAt;
        archive.ArchivedAt = search.ArchivedAt ?? now;
        archive.PayloadCompression = ConflictArchiveCompression.Gzip;
        archive.PayloadBytes = payloadBytes;

        archive.Hits.AddRange(payload.Results.Select(result => new ConflictSearchHitArchive
        {
            ConflictSearchId = search.Id,
            ConflictSearchResultId = result.Id,
            SearchNumber = search.SearchNumber,
            SearchTerm = result.SearchTerm,
            MatchedName = result.MatchedName,
            MatchedOn = result.MatchedOn,
            MatchType = result.MatchType,
            PartyRole = result.PartyRole,
            PartyId = result.PartyId,
            PartyName = result.PartyName,
            MatterId = result.MatterId,
            MatterNumber = result.MatterNumber,
            MatterName = result.MatterName,
            ClientId = result.ClientId,
            ClientNumber = result.ClientNumber,
            ClientName = result.ClientName,
            Score = result.Score,
            RiskLevel = result.RiskLevel,
            Explanation = result.Explanation,
            AiAssessment = result.AiAssessment,
            ClearanceStatus = result.ClearanceStatus,
            ClearanceNotes = result.ClearanceNotes,
            ClearedByUserId = result.ClearedByUserId,
            ClearedByDisplayName = result.ClearedByDisplayName,
            ClearedAt = result.ClearedAt,
            CreatedAt = result.CreatedAt,
            SearchableText = BuildSearchableText(result),
            NormalizedSearchableText = ConflictSearchService.NormalizeName(BuildSearchableText(result))
        }));

        search.ArchivedAt = archive.ArchivedAt;
        if (search.Results.Count > 0)
        {
            db.ConflictSearchResults.RemoveRange(search.Results);
            search.Results.Clear();
        }
    }

    public async Task RemoveArchiveAsync(Guid searchId)
    {
        await db.ConflictSearchHitArchives
            .Where(x => x.ConflictSearchId == searchId)
            .ExecuteDeleteAsync();

        await db.ConflictSearchArchives
            .Where(x => x.ConflictSearchId == searchId)
            .ExecuteDeleteAsync();

        await db.ConflictSearches
            .Where(x => x.Id == searchId)
            .ExecuteUpdateAsync(updates => updates.SetProperty(x => x.ArchivedAt, (DateTimeOffset?)null));
    }

    public async Task<ConflictSearchArchive?> GetArchiveAsync(Guid searchId)
    {
        return await db.ConflictSearchArchives
            .AsNoTracking()
            .Include(x => x.Hits)
            .FirstOrDefaultAsync(x => x.ConflictSearchId == searchId);
    }

    public ConflictSearchArchivePayload DecompressPayload(ConflictSearchArchive archive)
    {
        var json = Decompress(archive.PayloadBytes);
        return JsonSerializer.Deserialize<ConflictSearchArchivePayload>(json, PayloadJsonOptions)
            ?? throw new InvalidOperationException($"Conflict archive payload {archive.Id} could not be read.");
    }

    private static ConflictSearchArchivePayload BuildPayload(ConflictSearch search)
    {
        return new ConflictSearchArchivePayload(
            search.Id,
            search.SearchNumber,
            search.SearchName,
            search.SearchTerms,
            search.NormalizedTerms,
            search.FormSubmissionId,
            search.FormSubmission?.SubmissionNumber,
            search.FormSubmission?.FormDefinition?.Name ?? string.Empty,
            search.MatterId,
            search.Matter?.MatterNumber,
            search.Matter?.Name ?? string.Empty,
            search.Matter?.ClientId,
            search.Matter?.Client?.ClientNumber,
            search.Matter?.Client?.Name ?? string.Empty,
            search.RequestedByUserId,
            search.RequestedByUser?.DisplayName ?? "System",
            search.Status,
            search.ReviewerDecision,
            search.ReviewedByUserId,
            search.ReviewedByUser?.DisplayName ?? string.Empty,
            search.ReviewedAt,
            search.ReviewNotes,
            search.AiSummary,
            search.CreatedAt,
            search.UpdatedAt,
            search.Results
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.MatchedName)
                .Select(BuildResultPayload)
                .ToList());
    }

    private static ConflictSearchArchiveResultPayload BuildResultPayload(ConflictSearchResult result)
    {
        var matter = result.Matter;
        var client = result.Client ?? matter?.Client;
        return new ConflictSearchArchiveResultPayload(
            result.Id,
            result.PartyId,
            result.Party?.Name ?? string.Empty,
            result.MatterId,
            matter?.MatterNumber,
            matter?.Name ?? string.Empty,
            result.ClientId ?? matter?.ClientId,
            client?.ClientNumber,
            client?.Name ?? string.Empty,
            result.SearchTerm,
            result.MatchedName,
            result.MatchedOn,
            result.MatchType,
            result.PartyRole,
            result.Score,
            result.RiskLevel,
            result.Explanation,
            result.AiAssessment,
            result.ClearanceStatus,
            result.ClearanceNotes,
            result.ClearedByUserId,
            result.ClearedByUser?.DisplayName ?? string.Empty,
            result.ClearedAt,
            result.CreatedAt);
    }

    private static byte[] Compress(byte[] input)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize))
        {
            gzip.Write(input, 0, input.Length);
        }

        return output.ToArray();
    }

    private static byte[] Decompress(byte[] input)
    {
        using var source = new MemoryStream(input);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static string BuildSearchableText(ConflictSearchArchivePayload payload)
    {
        var values = new List<string?>
        {
            payload.SearchNumber.ToString(),
            payload.SearchName,
            payload.SearchTerms,
            payload.NormalizedTerms,
            payload.FormName,
            payload.MatterNumber?.ToString(),
            payload.MatterName,
            payload.ClientNumber?.ToString(),
            payload.ClientName,
            payload.Status,
            payload.ReviewerDecision,
            payload.ReviewNotes,
            payload.AiSummary,
            payload.RequestedByDisplayName,
            payload.ReviewedByDisplayName
        };

        foreach (var result in payload.Results)
        {
            values.Add(BuildSearchableText(result));
        }

        return string.Join(' ', values.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string BuildSearchableText(ConflictSearchArchiveResultPayload result)
    {
        return string.Join(' ', new string?[]
        {
            result.SearchTerm,
            result.MatchedName,
            result.MatchedOn,
            result.MatchType,
            result.PartyRole,
            result.PartyName,
            result.MatterNumber?.ToString(),
            result.MatterName,
            result.ClientNumber?.ToString(),
            result.ClientName,
            result.Score.ToString(),
            result.RiskLevel,
            result.Explanation,
            result.AiAssessment,
            result.ClearanceStatus,
            result.ClearanceNotes,
            result.ClearedByDisplayName
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}

public sealed record ConflictSearchArchivePayload(
    Guid Id,
    int SearchNumber,
    string SearchName,
    string SearchTerms,
    string NormalizedTerms,
    Guid? FormSubmissionId,
    int? SubmissionNumber,
    string FormName,
    Guid? MatterId,
    int? MatterNumber,
    string MatterName,
    Guid? ClientId,
    int? ClientNumber,
    string ClientName,
    Guid? RequestedByUserId,
    string RequestedByDisplayName,
    string Status,
    string ReviewerDecision,
    Guid? ReviewedByUserId,
    string ReviewedByDisplayName,
    DateTimeOffset? ReviewedAt,
    string ReviewNotes,
    string AiSummary,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ConflictSearchArchiveResultPayload> Results);

public sealed record ConflictSearchArchiveResultPayload(
    Guid Id,
    Guid? PartyId,
    string PartyName,
    Guid? MatterId,
    int? MatterNumber,
    string MatterName,
    Guid? ClientId,
    int? ClientNumber,
    string ClientName,
    string SearchTerm,
    string MatchedName,
    string MatchedOn,
    string MatchType,
    string PartyRole,
    int Score,
    string RiskLevel,
    string Explanation,
    string AiAssessment,
    string ClearanceStatus,
    string ClearanceNotes,
    Guid? ClearedByUserId,
    string ClearedByDisplayName,
    DateTimeOffset? ClearedAt,
    DateTimeOffset CreatedAt);
