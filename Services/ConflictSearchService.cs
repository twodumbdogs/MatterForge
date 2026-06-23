using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using CMIForge.Data;
using CMIForge.Models;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public class ConflictSearchService(CMIForgeDbContext db, ConflictSearchArchiveService archiveService, IConfiguration? configuration = null)
{
    public ConflictSearchService(CMIForgeDbContext db)
        : this(db, new ConflictSearchArchiveService(db), null)
    {
    }

    private const string AzureSearchCandidateProviderEnabledSettingKey = "Conflicts.AzureAiSearchCandidateProviderEnabled";
    private const string AzureSearchApiVersion = "2026-04-01";
    private const int DefaultCandidateLimit = 600;

    private static readonly HashSet<string> CorporateSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the",
        "co", "company", "corp", "corporation",
        "inc", "incorporated",
        "llc", "llp", "lp", "ltd", "limited",
        "plc", "pllc", "pc", "pa",
        "holdings", "holding", "group"
    };

    private static readonly HashSet<string> ConnectorWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and"
    };

    public static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var prepared = value.Trim()
            .Replace("&", " and ", StringComparison.Ordinal)
            .Replace("+", " and ", StringComparison.Ordinal);

        var decomposed = prepared.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var chars = decomposed
            .Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark)
            .Select(x => char.IsLetterOrDigit(x) ? x : ' ')
            .ToArray();

        var tokens = new string(chars)
            .Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (tokens.Count > 1 && tokens.All(x => x.Length == 1))
        {
            return string.Concat(tokens);
        }

        return string.Join(' ', tokens.Where(x => !CorporateSuffixes.Contains(x)));
    }

    public static List<string> SplitSearchTerms(string searchTerms)
    {
        return (searchTerms ?? string.Empty)
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(NormalizeName(x)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ConflictSearch> CreateAndRunSearchAsync(
        string searchName,
        string searchTerms,
        Guid? formSubmissionId,
        Guid? matterId,
        Guid? requestedByUserId)
    {
        var nextSearchNumber = (await db.ConflictSearches.MaxAsync(x => (int?)x.SearchNumber) ?? 0) + 1;
        var search = new ConflictSearch
        {
            SearchNumber = nextSearchNumber,
            SearchName = string.IsNullOrWhiteSpace(searchName) ? $"Conflict Search {RecordNumbers.ConflictSearch(nextSearchNumber)}" : searchName.Trim(),
            SearchTerms = searchTerms.Trim(),
            FormSubmissionId = formSubmissionId,
            MatterId = matterId,
            RequestedByUserId = requestedByUserId
        };

        await RunSearchAsync(search);
        db.ConflictSearches.Add(search);
        await db.SaveChangesAsync();
        return search;
    }

    public async Task RunSearchAsync(ConflictSearch search, bool includeHistory = true)
    {
        if (search.ArchivedAt.HasValue)
        {
            await archiveService.RemoveArchiveAsync(search.Id);
        }

        var terms = SplitSearchTerms(search.SearchTerms);
        search.NormalizedTerms = string.Join(Environment.NewLine, terms.Select(NormalizeName));
        search.Status = ConflictSearchStatuses.PendingReview;
        search.ReviewerDecision = ConflictSearchDecisions.Pending;
        search.ArchivedAt = null;
        search.UpdatedAt = DateTimeOffset.UtcNow;

        var searchEntry = db.Entry(search);
        if (search.Id != Guid.Empty && searchEntry.State != EntityState.Added && searchEntry.State != EntityState.Detached)
        {
            DetachTrackedResults(search.Id);
            search.Results.Clear();
            await db.ConflictSearchResults
                .Where(x => x.ConflictSearchId == search.Id)
                .ExecuteDeleteAsync();
        }

        var results = new Dictionary<string, ConflictSearchResult>();
        var candidateDocumentIds = await FindCandidateDocumentIdsAsync(terms, includeHistory);
        if (candidateDocumentIds is not null)
        {
            await AddCandidateDocumentResultsAsync(results, search, terms, candidateDocumentIds, includeHistory);
        }
        else
        {
            await AddFullScanResultsAsync(results, search, terms, includeHistory);
        }

        search.Results.Clear();
        foreach (var result in results.Values.OrderByDescending(x => x.Score).ThenBy(x => x.MatchedName))
        {
            result.RiskLevel = DetermineRiskLevel(result);
            result.Explanation = BuildExplanation(result);
            result.AiAssessment = BuildAiAssessment(result);
            search.Results.Add(result);
        }

        search.AiSummary = BuildAiSummary(search, terms);
    }

    private async Task AddFullScanResultsAsync(
        Dictionary<string, ConflictSearchResult> results,
        ConflictSearch search,
        List<string> terms,
        bool includeHistory)
    {
        var parties = await db.Parties
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Aliases)
            .Include(x => x.MatterParties)
                .ThenInclude(x => x.Matter)
                    .ThenInclude(x => x!.Client)
            .OrderBy(x => x.PartyNumber)
            .ToListAsync();
        var partiesById = parties.ToDictionary(x => x.Id);

        var relationships = await db.PartyRelationships
            .AsNoTracking()
            .Include(x => x.FromParty)
            .Include(x => x.ToParty)
            .ToListAsync();
        var clients = await db.Clients
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Aliases)
            .Include(x => x.Matters)
            .OrderBy(x => x.ClientNumber)
            .ToListAsync();
        var matters = await db.Matters
            .AsNoTracking()
            .Include(x => x.Client)
            .OrderBy(x => x.MatterNumber)
            .ToListAsync();

        foreach (var term in terms)
        {
            var normalizedTerm = NormalizeName(term);
            foreach (var party in parties)
            {
                var best = BestPartyMatch(term, normalizedTerm, party);
                if (best.Score < 45)
                {
                    continue;
                }

                AddPartyResults(results, search.Id, term, party, best.Score, best.MatchedName, best.MatchedOn, best.MatchType, relationshipType: null);

                if (best.Score < 70)
                {
                    continue;
                }

                var relatedRelationships = relationships
                    .Where(x => x.FromPartyId == party.Id || x.ToPartyId == party.Id)
                    .ToList();

                foreach (var relationship in relatedRelationships)
                {
                    var relatedPartyId = relationship.FromPartyId == party.Id ? relationship.ToPartyId : relationship.FromPartyId;
                    var relatedParty = partiesById.GetValueOrDefault(relatedPartyId);
                    if (relatedParty is null || relatedParty.Id == party.Id)
                    {
                        continue;
                    }

                    var relatedScore = Math.Max(45, best.Score - 20);
                    AddPartyResults(
                        results,
                        search.Id,
                        term,
                        relatedParty,
                        relatedScore,
                        relatedParty.Name,
                        $"{party.Name} relationship",
                        "Relationship expansion",
                        relationship.RelationshipType);
                }
            }

            foreach (var client in clients)
            {
                var bestClient = BestClientMatch(term, normalizedTerm, client);
                if (bestClient.Score < 45)
                {
                    continue;
                }

                AddClientResults(results, search.Id, term, client, bestClient.Score, bestClient.MatchedName, bestClient.MatchedOn, bestClient.MatchType);
            }

            foreach (var matter in matters)
            {
                var bestMatter = BestMatterMatch(term, normalizedTerm, matter);
                if (bestMatter.Score < 45)
                {
                    continue;
                }

                AddMatterResult(results, search.Id, term, matter, bestMatter.Score, bestMatter.MatchedName, bestMatter.MatchedOn, bestMatter.MatchType);
            }
        }

        if (includeHistory)
        {
            await AddHistoricalResultsAsync(results, search, terms);
        }
    }

    private async Task<List<Guid>?> FindCandidateDocumentIdsAsync(List<string> terms, bool includeHistory)
    {
        if (terms.Count == 0 || !db.Database.IsSqlServer())
        {
            return null;
        }

        var rebuilt = await EnsureConflictSearchDocumentsAsync(includeHistory);
        if (rebuilt)
        {
            return null;
        }

        var fullTextQuery = BuildFullTextQuery(terms);
        if (string.IsNullOrWhiteSpace(fullTextQuery))
        {
            return null;
        }

        var azureSearchCandidateIds = await FindAzureSearchCandidateDocumentIdsAsync(terms, includeHistory);
        if (azureSearchCandidateIds is not null)
        {
            return azureSearchCandidateIds;
        }

        var historyFilter = includeHistory
            ? string.Empty
            : $" AND [d].[SourceType] NOT IN ({string.Join(", ", HistoryDocumentTypes.Select((_, index) => $"@historyType{index}"))})";

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT TOP (600) [d].[Id]
            FROM CONTAINSTABLE([ConflictSearchDocuments], ([SearchableText], [NormalizedSearchableText]), @searchText, 600) AS [ft]
            INNER JOIN [ConflictSearchDocuments] AS [d] ON [d].[Id] = [ft].[KEY]
            WHERE 1 = 1{historyFilter}
            ORDER BY [ft].[RANK] DESC, [d].[SortNumber] ASC
            """;

        var searchParameter = command.CreateParameter();
        searchParameter.ParameterName = "@searchText";
        searchParameter.Value = fullTextQuery;
        command.Parameters.Add(searchParameter);

        if (!includeHistory)
        {
            for (var i = 0; i < HistoryDocumentTypes.Length; i++)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@historyType{i}";
                parameter.Value = HistoryDocumentTypes[i];
                command.Parameters.Add(parameter);
            }
        }

        try
        {
            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                ids.Add(reader.GetGuid(0));
            }

            return ids;
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<Guid>?> FindAzureSearchCandidateDocumentIdsAsync(List<string> terms, bool includeHistory)
    {
        if (!await IsAzureSearchCandidateProviderEnabledAsync())
        {
            return null;
        }

        var endpoint = configuration?["AzureSearch:Endpoint"]?.Trim().TrimEnd('/');
        var apiKey = configuration?["AzureSearch:ApiKey"]?.Trim();
        var indexName = ResolveAzureSearchIndexName();
        if (string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(indexName) ||
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            return null;
        }

        var maxCandidates = Math.Clamp(
            configuration?.GetValue("AzureSearch:MaxCandidates", DefaultCandidateLimit) ?? DefaultCandidateLimit,
            1,
            1000);
        var ids = new List<Guid>();
        var seen = new HashSet<Guid>();
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        client.DefaultRequestHeaders.Add("api-key", apiKey);

        foreach (var term in terms)
        {
            var searchText = BuildAzureSearchQuery(term);
            if (string.IsNullOrWhiteSpace(searchText))
            {
                continue;
            }

            var request = new AzureSearchRequest(
                Search: searchText,
                Top: maxCandidates,
                Select: "id",
                SearchFields: "matchedName,matchedOn,searchableText,normalizedSearchableText",
                SearchMode: "all",
                QueryType: "simple",
                Filter: includeHistory ? null : BuildAzureSearchActiveOnlyFilter(),
                OrderBy: "search.score() desc, sortNumber asc");

            try
            {
                var response = await client.PostAsJsonAsync(
                    new Uri(endpointUri, $"/indexes/{Uri.EscapeDataString(indexName)}/docs/search?api-version={AzureSearchApiVersion}"),
                    request);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var results = await response.Content.ReadFromJsonAsync<AzureSearchResponse>();
                if (results is null)
                {
                    return null;
                }

                foreach (var document in results.Value)
                {
                    if (Guid.TryParse(document.Id, out var id) && seen.Add(id))
                    {
                        ids.Add(id);
                    }
                }
            }
            catch
            {
                return null;
            }

            if (ids.Count >= maxCandidates)
            {
                break;
            }
        }

        return ids.Take(maxCandidates).ToList();
    }

    private async Task<bool> IsAzureSearchCandidateProviderEnabledAsync()
    {
        var value = await db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key == AzureSearchCandidateProviderEnabledSettingKey)
            .Select(x => x.Value)
            .FirstOrDefaultAsync();

        return bool.TryParse(value, out var enabled) && enabled;
    }

    private string? ResolveAzureSearchIndexName()
    {
        var configured = configuration?["AzureSearch:ConflictDocumentsIndex"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        var databaseName = db.Database.GetDbConnection().Database;
        return string.IsNullOrWhiteSpace(databaseName)
            ? null
            : $"{databaseName.Trim().ToLowerInvariant()}-conflict-documents";
    }

    private static string BuildAzureSearchQuery(string term)
    {
        var normalized = NormalizeName(term);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Select(x => $"{EscapeAzureSearchSimpleToken(x)}*")
            .ToList();

        return string.Join(' ', tokens);
    }

    private static string EscapeAzureSearchSimpleToken(string token)
    {
        var builder = new StringBuilder(token.Length);
        foreach (var ch in token)
        {
            if (ch is '+' or '|' or '"' or '(' or ')' or '\'' or '\\' or '/')
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static string BuildAzureSearchActiveOnlyFilter()
    {
        return string.Join(
            " and ",
            HistoryDocumentTypes.Select(x => $"sourceType ne '{x.Replace("'", "''", StringComparison.Ordinal)}'"));
    }

    private async Task AddCandidateDocumentResultsAsync(
        Dictionary<string, ConflictSearchResult> results,
        ConflictSearch search,
        List<string> terms,
        List<Guid> candidateDocumentIds,
        bool includeHistory)
    {
        if (candidateDocumentIds.Count == 0)
        {
            return;
        }

        var documents = await db.ConflictSearchDocuments
            .AsNoTracking()
            .Where(x => candidateDocumentIds.Contains(x.Id))
            .OrderBy(x => x.SortNumber)
            .ToListAsync();

        var partyIds = documents
            .Where(x => x.PartyId.HasValue)
            .Select(x => x.PartyId!.Value)
            .Distinct()
            .ToList();
        var clientIds = documents
            .Where(x => x.ClientId.HasValue &&
                (x.SourceType == ConflictSearchDocumentSourceTypes.Client ||
                    x.SourceType == ConflictSearchDocumentSourceTypes.ClientAlias))
            .Select(x => x.ClientId!.Value)
            .Distinct()
            .ToList();
        var matterIds = documents
            .Where(x => x.MatterId.HasValue && x.SourceType == ConflictSearchDocumentSourceTypes.Matter)
            .Select(x => x.MatterId!.Value)
            .Distinct()
            .ToList();

        var parties = partyIds.Count == 0
            ? []
            : await db.Parties
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.Aliases)
                .Include(x => x.MatterParties)
                    .ThenInclude(x => x.Matter)
                        .ThenInclude(x => x!.Client)
                .Where(x => partyIds.Contains(x.Id))
                .OrderBy(x => x.PartyNumber)
                .ToListAsync();
        var partiesById = parties.ToDictionary(x => x.Id);

        var clients = clientIds.Count == 0
            ? []
            : await db.Clients
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.Aliases)
                .Include(x => x.Matters)
                .Where(x => clientIds.Contains(x.Id))
                .OrderBy(x => x.ClientNumber)
                .ToListAsync();

        var matters = matterIds.Count == 0
            ? []
            : await db.Matters
                .AsNoTracking()
                .Include(x => x.Client)
                .Where(x => matterIds.Contains(x.Id))
                .OrderBy(x => x.MatterNumber)
                .ToListAsync();

        var relationships = partyIds.Count == 0
            ? []
            : await db.PartyRelationships
                .AsNoTracking()
                .Include(x => x.FromParty)
                .Include(x => x.ToParty)
                .Where(x => partyIds.Contains(x.FromPartyId) || partyIds.Contains(x.ToPartyId))
                .ToListAsync();

        var relatedPartyIds = relationships
            .SelectMany(x => new[] { x.FromPartyId, x.ToPartyId })
            .Where(x => !partiesById.ContainsKey(x))
            .Distinct()
            .ToList();
        if (relatedPartyIds.Count > 0)
        {
            var relatedParties = await db.Parties
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.Aliases)
                .Include(x => x.MatterParties)
                    .ThenInclude(x => x.Matter)
                        .ThenInclude(x => x!.Client)
                .Where(x => relatedPartyIds.Contains(x.Id))
                .ToListAsync();

            foreach (var relatedParty in relatedParties)
            {
                partiesById[relatedParty.Id] = relatedParty;
            }
        }

        foreach (var term in terms)
        {
            var normalizedTerm = NormalizeName(term);
            foreach (var party in parties)
            {
                var best = BestPartyMatch(term, normalizedTerm, party);
                if (best.Score < 45)
                {
                    continue;
                }

                AddPartyResults(results, search.Id, term, party, best.Score, best.MatchedName, best.MatchedOn, best.MatchType, relationshipType: null);

                if (best.Score < 70)
                {
                    continue;
                }

                foreach (var relationship in relationships.Where(x => x.FromPartyId == party.Id || x.ToPartyId == party.Id))
                {
                    var relatedPartyId = relationship.FromPartyId == party.Id ? relationship.ToPartyId : relationship.FromPartyId;
                    if (!partiesById.TryGetValue(relatedPartyId, out var relatedParty) || relatedParty.Id == party.Id)
                    {
                        continue;
                    }

                    var relatedScore = Math.Max(45, best.Score - 20);
                    AddPartyResults(
                        results,
                        search.Id,
                        term,
                        relatedParty,
                        relatedScore,
                        relatedParty.Name,
                        $"{party.Name} relationship",
                        "Relationship expansion",
                        relationship.RelationshipType);
                }
            }

            foreach (var client in clients)
            {
                var bestClient = BestClientMatch(term, normalizedTerm, client);
                if (bestClient.Score < 45)
                {
                    continue;
                }

                AddClientResults(results, search.Id, term, client, bestClient.Score, bestClient.MatchedName, bestClient.MatchedOn, bestClient.MatchType);
            }

            foreach (var matter in matters)
            {
                var bestMatter = BestMatterMatch(term, normalizedTerm, matter);
                if (bestMatter.Score < 45)
                {
                    continue;
                }

                AddMatterResult(results, search.Id, term, matter, bestMatter.Score, bestMatter.MatchedName, bestMatter.MatchedOn, bestMatter.MatchType);
            }
        }

        if (!includeHistory)
        {
            return;
        }

        var historicalDocuments = documents
            .Where(x => HistoryDocumentTypes.Contains(x.SourceType))
            .ToList();
        foreach (var document in historicalDocuments)
        {
            AddHistoricalTextMatches(
                results,
                search.Id,
                terms,
                new HistoricalConflictText(
                    document.MatterId,
                    document.ClientId,
                    document.MatchedName,
                    document.MatchedOn,
                    document.MatchType,
                    document.PartyRole,
                    document.SearchableText));
        }
    }

    private void DetachTrackedResults(Guid searchId)
    {
        var trackedResults = db.ChangeTracker
            .Entries<ConflictSearchResult>()
            .Where(x => x.Entity.ConflictSearchId == searchId)
            .ToList();

        foreach (var entry in trackedResults)
        {
            entry.State = EntityState.Detached;
        }
    }

    public async Task<ConflictPreview> PreviewAsync(string searchTerms, int maxResults = 8)
    {
        var previewSearch = new ConflictSearch
        {
            SearchName = "Live conflict preview",
            SearchTerms = searchTerms?.Trim() ?? string.Empty
        };

        var terms = SplitSearchTerms(previewSearch.SearchTerms);
        if (terms.Count == 0)
        {
            return new ConflictPreview(
                [],
                "Start typing a client, party, parent company, or opposing counsel name.",
                [],
                0,
                0,
                0,
                0,
                0);
        }

        await RunSearchAsync(previewSearch, includeHistory: false);

        var results = previewSearch.Results
            .OrderByDescending(x => Array.IndexOf(ConflictRiskLevels.All, x.RiskLevel))
            .ThenByDescending(x => x.Score)
            .ThenBy(x => x.MatchedName)
            .Take(maxResults)
            .ToList();

        var matterIds = results
            .Where(x => x.MatterId.HasValue)
            .Select(x => x.MatterId!.Value)
            .Distinct()
            .ToList();
        var clientIds = results
            .Where(x => x.ClientId.HasValue)
            .Select(x => x.ClientId!.Value)
            .Distinct()
            .ToList();

        var matters = await db.Matters
            .AsNoTracking()
            .Include(x => x.Client)
            .Where(x => matterIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        var clients = await db.Clients
            .AsNoTracking()
            .Where(x => clientIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var items = results.Select(result =>
        {
            matters.TryGetValue(result.MatterId ?? Guid.Empty, out var matter);
            clients.TryGetValue(result.ClientId ?? Guid.Empty, out var client);

            return new ConflictPreviewItem(
                result.SearchTerm,
                result.MatchedName,
                result.MatchedOn,
                result.MatchType,
                result.PartyRole,
                result.Score,
                result.RiskLevel,
                result.Explanation,
                result.AiAssessment,
                result.PartyId,
                result.MatterId,
                matter?.MatterNumber,
                matter?.Name ?? string.Empty,
                result.ClientId,
                client?.ClientNumber ?? matter?.Client?.ClientNumber,
                client?.Name ?? matter?.Client?.Name ?? string.Empty);
        }).ToList();

        var criticalCount = previewSearch.Results.Count(x => x.RiskLevel == ConflictRiskLevels.Critical);
        var highCount = previewSearch.Results.Count(x => x.RiskLevel == ConflictRiskLevels.High);
        var relationshipCount = previewSearch.Results.Count(x =>
            x.MatchType.Contains("Relationship", StringComparison.OrdinalIgnoreCase) ||
            x.MatchedOn.Contains("relationship", StringComparison.OrdinalIgnoreCase));
        var priorSearchCount = previewSearch.Results.Count(x =>
            x.PartyRole.StartsWith("Prior ", StringComparison.OrdinalIgnoreCase));

        return new ConflictPreview(
            terms,
            BuildAiSummary(previewSearch, terms),
            items,
            previewSearch.Results.Count,
            criticalCount,
            highCount,
            relationshipCount,
            priorSearchCount);
    }

    public async Task ApplyReviewDecisionAsync(Guid searchId, string decision, string notes, Guid? reviewedByUserId, Guid? reviewedAsUserId = null)
    {
        var search = await db.ConflictSearches
            .Include(x => x.Results)
            .FirstOrDefaultAsync(x => x.Id == searchId);
        if (search is null)
        {
            return;
        }

        search.ReviewerDecision = ConflictSearchDecisions.All.Contains(decision) ? decision : ConflictSearchDecisions.Pending;
        search.Status = StatusForDecision(search.ReviewerDecision);
        search.ReviewNotes = notes?.Trim() ?? string.Empty;
        search.ReviewedByUserId = reviewedByUserId;
        search.ReviewedAt = DateTimeOffset.UtcNow;
        search.UpdatedAt = DateTimeOffset.UtcNow;

        if (search.ReviewerDecision == ConflictSearchDecisions.Clear)
        {
            foreach (var result in search.Results.Where(x => x.ClearanceStatus == ConflictSearchDecisions.Pending))
            {
                result.ClearanceStatus = ConflictSearchDecisions.Clear;
                result.ClearedByUserId = reviewedByUserId;
                result.ClearedAsUserId = reviewedAsUserId;
                result.ClearedAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync();
            await archiveService.ArchiveIfClearedAsync(search.Id);
        }

        await db.SaveChangesAsync();
    }

    public async Task ApplyResultClearanceAsync(Guid resultId, string status, string notes, Guid? reviewedByUserId, Guid? reviewedAsUserId = null)
    {
        var result = await db.ConflictSearchResults.FirstOrDefaultAsync(x => x.Id == resultId);
        if (result is null)
        {
            return;
        }

        var clearanceStatus = ConflictSearchDecisions.All.Contains(status) ? status : ConflictSearchDecisions.Pending;
        result.ClearanceStatus = clearanceStatus;
        result.ClearanceNotes = notes?.Trim() ?? string.Empty;
        result.ClearedByUserId = reviewedByUserId;
        result.ClearedAsUserId = reviewedAsUserId;
        result.ClearedAt = clearanceStatus == ConflictSearchDecisions.Pending && string.IsNullOrWhiteSpace(result.ClearanceNotes)
            ? null
            : DateTimeOffset.UtcNow;

        await RefreshSearchFromResultClearancesAsync(result.ConflictSearchId, reviewedByUserId);
        await db.SaveChangesAsync();
    }

    public async Task ApplyResultClearanceAsync(IEnumerable<Guid> resultIds, string status, string notes, Guid? reviewedByUserId, Guid? reviewedAsUserId = null)
    {
        var ids = resultIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var results = await db.ConflictSearchResults
            .Where(x => ids.Contains(x.Id))
            .ToListAsync();
        if (results.Count == 0)
        {
            return;
        }

        var clearanceStatus = ConflictSearchDecisions.All.Contains(status) ? status : ConflictSearchDecisions.Pending;
        foreach (var result in results)
        {
            result.ClearanceStatus = clearanceStatus;
            result.ClearanceNotes = notes?.Trim() ?? string.Empty;
            result.ClearedByUserId = reviewedByUserId;
            result.ClearedAsUserId = reviewedAsUserId;
            result.ClearedAt = clearanceStatus == ConflictSearchDecisions.Pending && string.IsNullOrWhiteSpace(result.ClearanceNotes)
                ? null
                : DateTimeOffset.UtcNow;
        }

        foreach (var searchId in results.Select(x => x.ConflictSearchId).Distinct())
        {
            await RefreshSearchFromResultClearancesAsync(searchId, reviewedByUserId);
        }

        await db.SaveChangesAsync();
    }

    public async Task<List<ConflictSearchResult>> EscalateResultsAsync(IEnumerable<Guid> resultIds, Guid escalatedToUserId, string notes, Guid? escalatedByUserId, Guid? escalatedAsUserId = null)
    {
        var ids = resultIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var results = await db.ConflictSearchResults
            .Include(x => x.ConflictSearch)
            .Include(x => x.EscalatedToUser)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync();
        if (results.Count == 0)
        {
            return [];
        }

        var trimmedNotes = notes?.Trim() ?? string.Empty;
        var now = DateTimeOffset.UtcNow;
        foreach (var result in results)
        {
            result.EscalatedToUserId = escalatedToUserId;
            result.EscalatedByUserId = escalatedByUserId;
            result.EscalatedAsUserId = escalatedAsUserId;
            result.EscalatedAt = now;
            result.EscalationNotes = trimmedNotes;
            result.EscalationApprovedAt = null;
            result.EscalationApprovedByUserId = null;
            result.EscalationApprovedAsUserId = null;
            result.EscalationApprovalNotes = string.Empty;
        }

        foreach (var searchId in results.Select(x => x.ConflictSearchId).Distinct())
        {
            var search = await db.ConflictSearches.FirstOrDefaultAsync(x => x.Id == searchId);
            if (search is not null)
            {
                search.UpdatedAt = now;
            }
        }

        await db.SaveChangesAsync();
        return results;
    }

    public async Task<ConflictSearchResult?> ApproveEscalationAsync(Guid resultId, string notes, Guid approvedByUserId, Guid? approvedAsUserId = null)
    {
        var authorizedReviewerId = approvedAsUserId ?? approvedByUserId;
        var result = await db.ConflictSearchResults
            .Include(x => x.ConflictSearch)
            .Include(x => x.EscalatedToUser)
            .FirstOrDefaultAsync(x => x.Id == resultId);
        if (result is null || result.EscalatedToUserId != authorizedReviewerId)
        {
            return null;
        }

        result.EscalationApprovedByUserId = approvedByUserId;
        result.EscalationApprovedAsUserId = approvedAsUserId;
        result.EscalationApprovedAt = DateTimeOffset.UtcNow;
        result.EscalationApprovalNotes = notes?.Trim() ?? string.Empty;
        if (result.ConflictSearch is not null)
        {
            result.ConflictSearch.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
        return result;
    }

    public async Task RerunSearchAsync(Guid searchId, string? additionalTerms)
    {
        var existingSearch = await db.ConflictSearches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == searchId);
        if (existingSearch is null)
        {
            return;
        }

        var existingTerms = SplitSearchTerms(existingSearch.SearchTerms);
        var newTerms = SplitSearchTerms(additionalTerms ?? string.Empty);
        var combinedTerms = existingTerms
            .Concat(newTerms)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var search = new ConflictSearch
        {
            Id = existingSearch.Id,
            SearchNumber = existingSearch.SearchNumber,
            SearchName = existingSearch.SearchName,
            SearchTerms = existingSearch.SearchTerms,
            FormSubmissionId = existingSearch.FormSubmissionId,
            MatterId = existingSearch.MatterId,
            RequestedByUserId = existingSearch.RequestedByUserId,
            CreatedAt = existingSearch.CreatedAt,
            ArchivedAt = existingSearch.ArchivedAt
        };

        if (combinedTerms.Count > 0)
        {
            search.SearchTerms = string.Join(Environment.NewLine, combinedTerms);
        }

        await RunSearchAsync(search);

        var results = search.Results.ToList();
        search.Results.Clear();

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            await db.ConflictSearchResults
                .Where(x => x.ConflictSearchId == search.Id)
                .ExecuteDeleteAsync();

            var updatedRows = await db.ConflictSearches
                .Where(x => x.Id == search.Id)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(x => x.SearchTerms, search.SearchTerms)
                    .SetProperty(x => x.NormalizedTerms, search.NormalizedTerms)
                    .SetProperty(x => x.Status, search.Status)
                    .SetProperty(x => x.ReviewerDecision, search.ReviewerDecision)
                    .SetProperty(x => x.ArchivedAt, search.ArchivedAt)
                    .SetProperty(x => x.UpdatedAt, search.UpdatedAt)
                    .SetProperty(x => x.AiSummary, search.AiSummary));

            if (updatedRows == 0)
            {
                throw new InvalidOperationException($"Conflict search '{search.Id}' could not be updated because it no longer exists.");
            }

            db.ConflictSearchResults.AddRange(results);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        });
    }

    public async Task RerunSearchAsync(ConflictSearch search, string? additionalTerms)
    {
        await RerunSearchAsync(search.Id, additionalTerms);
    }

    private async Task RefreshSearchFromResultClearancesAsync(Guid searchId, Guid? reviewedByUserId)
    {
        var search = await db.ConflictSearches
            .Include(x => x.Results)
            .FirstOrDefaultAsync(x => x.Id == searchId);
        if (search is null || search.Results.Count == 0)
        {
            return;
        }

        var resultStatuses = search.Results
            .Select(x => ConflictSearchDecisions.All.Contains(x.ClearanceStatus) ? x.ClearanceStatus : ConflictSearchDecisions.Pending)
            .ToList();

        var aggregateDecision = ConflictSearchDecisions.Pending;
        if (resultStatuses.Contains(ConflictSearchDecisions.Conflict))
        {
            aggregateDecision = ConflictSearchDecisions.Conflict;
        }
        else if (resultStatuses.Contains(ConflictSearchDecisions.PotentialConflict))
        {
            aggregateDecision = ConflictSearchDecisions.PotentialConflict;
        }
        else if (resultStatuses.Contains(ConflictSearchDecisions.NeedsInfo))
        {
            aggregateDecision = ConflictSearchDecisions.NeedsInfo;
        }
        else if (resultStatuses.All(x => x == ConflictSearchDecisions.Clear))
        {
            aggregateDecision = ConflictSearchDecisions.Clear;
        }

        search.ReviewerDecision = aggregateDecision;
        search.Status = StatusForDecision(aggregateDecision);
        if (aggregateDecision != ConflictSearchDecisions.Pending)
        {
            search.ReviewedByUserId = reviewedByUserId;
            search.ReviewedAt = DateTimeOffset.UtcNow;
        }

        search.UpdatedAt = DateTimeOffset.UtcNow;

        if (aggregateDecision == ConflictSearchDecisions.Clear)
        {
            await db.SaveChangesAsync();
            await archiveService.ArchiveIfClearedAsync(search.Id);
        }
    }

    private static string StatusForDecision(string decision)
    {
        return decision switch
        {
            ConflictSearchDecisions.Clear => ConflictSearchStatuses.Cleared,
            ConflictSearchDecisions.Conflict => ConflictSearchStatuses.Conflict,
            ConflictSearchDecisions.PotentialConflict => ConflictSearchStatuses.PotentialConflict,
            ConflictSearchDecisions.NeedsInfo => ConflictSearchStatuses.NeedsInfo,
            _ => ConflictSearchStatuses.PendingReview
        };
    }

    public async Task<Party> GetOrCreatePartyAsync(string name, string partyType = PartyTypes.Organization)
    {
        var normalizedName = NormalizeName(name);
        var trackedParty = db.Parties.Local.FirstOrDefault(x => x.NormalizedName == normalizedName);
        if (trackedParty is not null)
        {
            return trackedParty;
        }

        var existingParty = await db.Parties.FirstOrDefaultAsync(x => x.NormalizedName == normalizedName);
        if (existingParty is not null)
        {
            return existingParty;
        }

        var databaseMaxPartyNumber = await db.Parties.MaxAsync(x => (int?)x.PartyNumber) ?? 0;
        var localMaxPartyNumber = db.Parties.Local.Count == 0 ? 0 : db.Parties.Local.Max(x => x.PartyNumber);
        var nextPartyNumber = Math.Max(databaseMaxPartyNumber, localMaxPartyNumber) + 1;
        var party = new Party
        {
            PartyNumber = nextPartyNumber,
            Name = name.Trim(),
            NormalizedName = normalizedName,
            PartyType = partyType
        };

        db.Parties.Add(party);
        return party;
    }

    public async Task EnsureMatterClientPartyAsync(Client client, Matter matter)
    {
        var party = await GetOrCreatePartyAsync(client.Name, PartyTypes.Organization);

        var linked = await db.MatterParties.AnyAsync(x =>
            x.MatterId == matter.Id &&
            x.PartyId == party.Id &&
            x.Role == PartyRoles.Client);

        if (!linked)
        {
            db.MatterParties.Add(new MatterParty
            {
                MatterId = matter.Id,
                Party = party,
                Role = PartyRoles.Client,
                Notes = $"Synced from client {client.ClientNumber:D8}"
            });
        }
    }

    public async Task SyncExistingClientMatterPartiesAsync()
    {
        var matters = await db.Matters
            .Include(x => x.Client)
            .ToListAsync();

        foreach (var matter in matters.Where(x => x.Client is not null))
        {
            await EnsureMatterClientPartyAsync(matter.Client!, matter);
        }

        await db.SaveChangesAsync();
    }

    private static readonly string[] HistoryDocumentTypes =
    [
        ConflictSearchDocumentSourceTypes.PriorSearch,
        ConflictSearchDocumentSourceTypes.PriorResult,
        ConflictSearchDocumentSourceTypes.ArchivedResult
    ];

    private static readonly string[] LiveDocumentTypes =
    [
        ConflictSearchDocumentSourceTypes.Party,
        ConflictSearchDocumentSourceTypes.PartyAlias,
        ConflictSearchDocumentSourceTypes.Client,
        ConflictSearchDocumentSourceTypes.ClientAlias,
        ConflictSearchDocumentSourceTypes.Matter
    ];

    private async Task<bool> EnsureConflictSearchDocumentsAsync(bool includeHistory)
    {
        var expectedCount = await CountExpectedLiveConflictSearchDocumentsAsync();
        var currentCount = await db.ConflictSearchDocuments.CountAsync(x => LiveDocumentTypes.Contains(x.SourceType));

        if (expectedCount == currentCount && currentCount > 0)
        {
            return false;
        }

        await RebuildConflictSearchDocumentsAsync(includeHistory: false);
        return true;
    }

    private async Task<int> CountExpectedLiveConflictSearchDocumentsAsync()
    {
        return
            await db.Parties.CountAsync() +
            await db.PartyAliases.CountAsync() +
            await db.Clients.CountAsync() +
            await db.ClientAliases.CountAsync() +
            await db.Matters.CountAsync();
    }

    private async Task RebuildConflictSearchDocumentsAsync(bool includeHistory)
    {
        var sourceTypes = includeHistory ? LiveDocumentTypes.Concat(HistoryDocumentTypes).ToArray() : LiveDocumentTypes;
        await db.ConflictSearchDocuments
            .Where(x => sourceTypes.Contains(x.SourceType))
            .ExecuteDeleteAsync();

        var now = DateTimeOffset.UtcNow;
        var documents = new List<ConflictSearchDocument>();

        var parties = await db.Parties
            .AsNoTracking()
            .Include(x => x.Aliases)
            .OrderBy(x => x.PartyNumber)
            .ToListAsync();
        foreach (var party in parties)
        {
            documents.Add(new ConflictSearchDocument
            {
                SourceType = ConflictSearchDocumentSourceTypes.Party,
                SourceId = party.Id,
                PartyId = party.Id,
                MatchedName = party.Name,
                MatchedOn = "Party name",
                MatchType = "Name match",
                PartyRole = "Global party",
                SearchableText = JoinSearchableText(party.Name, party.NormalizedName, party.PartyType, party.Status, party.Notes, string.Join(' ', party.Aliases.Select(x => x.Alias))),
                NormalizedSearchableText = NormalizeName(JoinSearchableText(party.Name, party.NormalizedName, party.Notes, string.Join(' ', party.Aliases.Select(x => x.Alias)))),
                SortNumber = party.PartyNumber,
                CreatedAt = now,
                UpdatedAt = now
            });

            foreach (var alias in party.Aliases)
            {
                documents.Add(new ConflictSearchDocument
                {
                    SourceType = ConflictSearchDocumentSourceTypes.PartyAlias,
                    SourceId = alias.Id,
                    PartyId = party.Id,
                    MatchedName = alias.Alias,
                    MatchedOn = "Alias",
                    MatchType = "Alias match",
                    PartyRole = "Global party",
                    SearchableText = JoinSearchableText(alias.Alias, alias.NormalizedAlias, alias.Notes, party.Name, party.PartyType),
                    NormalizedSearchableText = NormalizeName(JoinSearchableText(alias.Alias, alias.NormalizedAlias, alias.Notes, party.Name)),
                    SortNumber = party.PartyNumber,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        var clients = await db.Clients
            .AsNoTracking()
            .Include(x => x.Aliases)
            .OrderBy(x => x.ClientNumber)
            .ToListAsync();
        foreach (var client in clients)
        {
            documents.Add(new ConflictSearchDocument
            {
                SourceType = ConflictSearchDocumentSourceTypes.Client,
                SourceId = client.Id,
                ClientId = client.Id,
                MatchedName = client.Name,
                MatchedOn = "Client name",
                MatchType = "Name match",
                PartyRole = "Client",
                SearchableText = JoinSearchableText(client.Name, client.PrimaryContact, client.Email, client.Notes, client.Status, string.Join(' ', client.Aliases.Select(x => x.Alias))),
                NormalizedSearchableText = NormalizeName(JoinSearchableText(client.Name, client.PrimaryContact, client.Notes, string.Join(' ', client.Aliases.Select(x => x.Alias)))),
                SortNumber = client.ClientNumber,
                CreatedAt = now,
                UpdatedAt = now
            });

            foreach (var alias in client.Aliases)
            {
                documents.Add(new ConflictSearchDocument
                {
                    SourceType = ConflictSearchDocumentSourceTypes.ClientAlias,
                    SourceId = alias.Id,
                    ClientId = client.Id,
                    MatchedName = alias.Alias,
                    MatchedOn = "Client alias",
                    MatchType = "Alias match",
                    PartyRole = "Client",
                    SearchableText = JoinSearchableText(alias.Alias, alias.NormalizedAlias, alias.Notes, client.Name),
                    NormalizedSearchableText = NormalizeName(JoinSearchableText(alias.Alias, alias.NormalizedAlias, alias.Notes, client.Name)),
                    SortNumber = client.ClientNumber,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        var matters = await db.Matters
            .AsNoTracking()
            .Include(x => x.Client)
            .OrderBy(x => x.MatterNumber)
            .ToListAsync();
        documents.AddRange(matters.Select(matter => new ConflictSearchDocument
        {
            SourceType = ConflictSearchDocumentSourceTypes.Matter,
            SourceId = matter.Id,
            MatterId = matter.Id,
            ClientId = matter.ClientId,
            MatchedName = matter.Name,
            MatchedOn = "Matter name",
            MatchType = "Name match",
            PartyRole = "Matter",
            SearchableText = JoinSearchableText(matter.Name, matter.PracticeArea, matter.Status, matter.Notes, matter.Client?.Name),
            NormalizedSearchableText = NormalizeName(JoinSearchableText(matter.Name, matter.PracticeArea, matter.Notes, matter.Client?.Name)),
            SortNumber = matter.MatterNumber,
            CreatedAt = now,
            UpdatedAt = now
        }));

        if (includeHistory)
        {
            await AddHistoricalConflictSearchDocumentsAsync(documents, now);
        }

        db.ConflictSearchDocuments.AddRange(documents);
        await db.SaveChangesAsync();
    }

    private async Task AddHistoricalConflictSearchDocumentsAsync(List<ConflictSearchDocument> documents, DateTimeOffset now)
    {
        var priorSearchRows = await db.ConflictSearches
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.SearchNumber,
                x.SearchName,
                x.SearchTerms,
                x.ReviewNotes,
                x.AiSummary,
                x.Status,
                x.ReviewerDecision,
                x.MatterId,
                MatterName = x.Matter == null ? null : x.Matter.Name,
                ClientId = x.Matter == null ? null : (Guid?)x.Matter.ClientId,
                ClientName = x.Matter == null || x.Matter.Client == null ? null : x.Matter.Client.Name
            })
            .ToListAsync();

        documents.AddRange(priorSearchRows.Select(row =>
        {
            var searchableText = JoinSearchableText(
                row.SearchName,
                row.SearchTerms,
                row.ReviewNotes,
                row.AiSummary,
                row.Status,
                row.ReviewerDecision,
                row.MatterName,
                row.ClientName);

            return new ConflictSearchDocument
            {
                SourceType = ConflictSearchDocumentSourceTypes.PriorSearch,
                SourceId = row.Id,
                MatterId = row.MatterId,
                ClientId = row.ClientId,
                MatchedName = $"Search {RecordNumbers.ConflictSearch(row.SearchNumber)}: {row.SearchName}",
                MatchedOn = "Prior conflict search",
                MatchType = "Prior search text match",
                PartyRole = "Prior search history",
                SearchableText = searchableText,
                NormalizedSearchableText = NormalizeName(searchableText),
                SortNumber = row.SearchNumber,
                CreatedAt = now,
                UpdatedAt = now
            };
        }));

        var priorResultRows = await db.ConflictSearchResults
            .AsNoTracking()
            .Where(x => x.ClearanceNotes != string.Empty)
            .Select(x => new
            {
                x.Id,
                SearchNumber = x.ConflictSearch == null ? 0 : x.ConflictSearch.SearchNumber,
                x.SearchTerm,
                x.MatchedName,
                x.PartyRole,
                x.ClearanceStatus,
                x.ClearanceNotes,
                x.MatterId,
                x.ClientId,
                PartyName = x.Party == null ? null : x.Party.Name,
                MatterName = x.Matter == null ? null : x.Matter.Name,
                ClientName = x.Client == null ? null : x.Client.Name
            })
            .ToListAsync();

        documents.AddRange(priorResultRows.Select(row =>
        {
            var searchableText = JoinSearchableText(
                row.ClearanceNotes,
                row.ClearanceStatus,
                row.SearchTerm,
                row.MatchedName,
                row.PartyRole,
                row.PartyName,
                row.MatterName,
                row.ClientName);

            return new ConflictSearchDocument
            {
                SourceType = ConflictSearchDocumentSourceTypes.PriorResult,
                SourceId = row.Id,
                MatterId = row.MatterId,
                ClientId = row.ClientId,
                MatchedName = $"Search {RecordNumbers.ConflictSearch(row.SearchNumber)} result: {row.MatchedName}",
                MatchedOn = "Prior result clearance notes",
                MatchType = "Prior result notes match",
                PartyRole = "Prior result history",
                SearchableText = searchableText,
                NormalizedSearchableText = NormalizeName(searchableText),
                SortNumber = row.SearchNumber,
                CreatedAt = now,
                UpdatedAt = now
            };
        }));

        var archivedResultRows = await db.ConflictSearchHitArchives
            .AsNoTracking()
            .Where(x => x.ClearanceNotes != string.Empty)
            .Select(x => new
            {
                x.Id,
                x.SearchNumber,
                x.SearchTerm,
                x.MatchedName,
                x.PartyRole,
                x.ClearanceStatus,
                x.ClearanceNotes,
                x.MatterId,
                x.ClientId,
                x.PartyName,
                x.MatterName,
                x.ClientName
            })
            .ToListAsync();

        documents.AddRange(archivedResultRows.Select(row =>
        {
            var searchableText = JoinSearchableText(
                row.ClearanceNotes,
                row.ClearanceStatus,
                row.SearchTerm,
                row.MatchedName,
                row.PartyRole,
                row.PartyName,
                row.MatterName,
                row.ClientName);

            return new ConflictSearchDocument
            {
                SourceType = ConflictSearchDocumentSourceTypes.ArchivedResult,
                SourceId = row.Id,
                MatterId = row.MatterId,
                ClientId = row.ClientId,
                MatchedName = $"Archived search {RecordNumbers.ConflictSearch(row.SearchNumber)} result: {row.MatchedName}",
                MatchedOn = "Archived result clearance notes",
                MatchType = "Archived result notes match",
                PartyRole = "Prior result history",
                SearchableText = searchableText,
                NormalizedSearchableText = NormalizeName(searchableText),
                SortNumber = row.SearchNumber,
                CreatedAt = now,
                UpdatedAt = now
            };
        }));
    }

    private static string BuildFullTextQuery(List<string> terms)
    {
        var clauses = terms
            .Select(term => SplitNormalizedTokens(NormalizeName(term))
                .Where(token => token.Length >= 2)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList())
            .Where(tokens => tokens.Count > 0)
            .Take(12)
            .Select(tokens =>
            {
                var prefixClause = string.Join(" AND ", tokens.Select(token => $"\"{EscapeFullTextToken(token)}*\""));
                if (tokens.Count == 1)
                {
                    return prefixClause;
                }

                var phraseClause = $"\"{string.Join(' ', tokens.Select(EscapeFullTextToken))}\"";
                return $"({phraseClause} OR ({prefixClause}))";
            })
            .ToList();

        return string.Join(" OR ", clauses);
    }

    private static string EscapeFullTextToken(string token)
    {
        return token.Replace("\"", "\"\"", StringComparison.Ordinal);
    }

    private static ConflictCandidate BestPartyMatch(string term, string normalizedTerm, Party party)
    {
        var candidates = new List<ConflictCandidate>
        {
            ScoreCandidate(term, normalizedTerm, party.Name, party.NormalizedName, "Party name", "Name match")
        };

        candidates.AddRange(party.Aliases.Select(alias =>
            ScoreCandidate(term, normalizedTerm, alias.Alias, alias.NormalizedAlias, "Alias", "Alias match")));

        return candidates.OrderByDescending(x => x.Score).First();
    }

    private static ConflictCandidate BestClientMatch(string term, string normalizedTerm, Client client)
    {
        var candidates = new List<ConflictCandidate>
        {
            ScoreCandidate(term, normalizedTerm, client.Name, NormalizeName(client.Name), "Client name", "Name match")
        };

        candidates.AddRange(client.Aliases.Select(alias =>
            ScoreCandidate(term, normalizedTerm, alias.Alias, alias.NormalizedAlias, "Client alias", "Alias match")));

        return candidates.OrderByDescending(x => x.Score).First();
    }

    private static ConflictCandidate BestMatterMatch(string term, string normalizedTerm, Matter matter)
    {
        return ScoreCandidate(term, normalizedTerm, matter.Name, NormalizeName(matter.Name), "Matter name", "Name match");
    }

    private async Task AddHistoricalResultsAsync(
        Dictionary<string, ConflictSearchResult> results,
        ConflictSearch search,
        List<string> terms)
    {
        if (terms.Count == 0)
        {
            return;
        }

        var priorSearchRows = await db.ConflictSearches
            .AsNoTracking()
            .Where(x => x.Id != search.Id)
            .Select(x => new
            {
                x.Id,
                x.SearchNumber,
                x.SearchName,
                x.SearchTerms,
                x.ReviewNotes,
                x.AiSummary,
                x.Status,
                x.ReviewerDecision,
                x.MatterId,
                MatterName = x.Matter == null ? null : x.Matter.Name,
                MatterNumber = x.Matter == null ? null : (int?)x.Matter.MatterNumber,
                ClientId = x.Matter == null ? null : (Guid?)x.Matter.ClientId,
                ClientName = x.Matter == null || x.Matter.Client == null ? null : x.Matter.Client.Name
            })
            .ToListAsync();

        foreach (var row in priorSearchRows)
        {
            var searchableText = JoinSearchableText(
                row.SearchName,
                row.SearchTerms,
                row.ReviewNotes,
                row.AiSummary,
                row.Status,
                row.ReviewerDecision,
                row.MatterName,
                row.ClientName);

            AddHistoricalTextMatches(
                results,
                search.Id,
                terms,
                new HistoricalConflictText(
                    row.MatterId,
                    row.ClientId,
                    $"Search {RecordNumbers.ConflictSearch(row.SearchNumber)}: {row.SearchName}",
                    "Prior conflict search",
                    "Prior search text match",
                    "Prior search history",
                    searchableText));
        }

        var priorResultRows = await db.ConflictSearchResults
            .AsNoTracking()
            .Where(x => x.ConflictSearchId != search.Id && x.ClearanceNotes != string.Empty)
            .Select(x => new
            {
                SearchNumber = x.ConflictSearch == null ? 0 : x.ConflictSearch.SearchNumber,
                SearchName = x.ConflictSearch == null ? string.Empty : x.ConflictSearch.SearchName,
                x.SearchTerm,
                x.MatchedName,
                x.PartyRole,
                x.ClearanceStatus,
                x.ClearanceNotes,
                x.MatterId,
                x.ClientId,
                PartyName = x.Party == null ? null : x.Party.Name,
                MatterName = x.Matter == null ? null : x.Matter.Name,
                ClientName = x.Client == null ? null : x.Client.Name
            })
            .ToListAsync();

        foreach (var row in priorResultRows)
        {
            var searchableText = JoinSearchableText(
                row.ClearanceNotes,
                row.ClearanceStatus,
                row.SearchTerm,
                row.MatchedName,
                row.PartyRole,
                row.PartyName,
                row.MatterName,
                row.ClientName);

            AddHistoricalTextMatches(
                results,
                search.Id,
                terms,
                new HistoricalConflictText(
                    row.MatterId,
                    row.ClientId,
                    $"Search {RecordNumbers.ConflictSearch(row.SearchNumber)} result: {row.MatchedName}",
                    "Prior result clearance notes",
                    "Prior result notes match",
                    "Prior result history",
                searchableText));
        }

        var archivedResultRows = await db.ConflictSearchHitArchives
            .AsNoTracking()
            .Where(x => x.ConflictSearchId != search.Id && x.ClearanceNotes != string.Empty)
            .Select(x => new
            {
                x.SearchNumber,
                SearchName = x.ConflictSearchArchive == null ? string.Empty : x.ConflictSearchArchive.SearchName,
                x.SearchTerm,
                x.MatchedName,
                x.PartyRole,
                x.ClearanceStatus,
                x.ClearanceNotes,
                x.MatterId,
                x.ClientId,
                x.PartyName,
                x.MatterName,
                x.ClientName
            })
            .ToListAsync();

        foreach (var row in archivedResultRows)
        {
            var searchableText = JoinSearchableText(
                row.ClearanceNotes,
                row.ClearanceStatus,
                row.SearchTerm,
                row.MatchedName,
                row.PartyRole,
                row.PartyName,
                row.MatterName,
                row.ClientName);

            AddHistoricalTextMatches(
                results,
                search.Id,
                terms,
                new HistoricalConflictText(
                    row.MatterId,
                    row.ClientId,
                    $"Archived search {RecordNumbers.ConflictSearch(row.SearchNumber)} result: {row.MatchedName}",
                    "Archived result clearance notes",
                    "Archived result notes match",
                    "Prior result history",
                    searchableText));
        }
    }

    private static void AddHistoricalTextMatches(
        Dictionary<string, ConflictSearchResult> results,
        Guid conflictSearchId,
        List<string> terms,
        HistoricalConflictText candidate)
    {
        var normalizedCandidate = NormalizeName(candidate.SearchableText);
        if (string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return;
        }

        foreach (var term in terms)
        {
            var normalizedTerm = NormalizeName(term);
            var score = CalculateScore(normalizedTerm, normalizedCandidate);
            if (score < 60)
            {
                continue;
            }

            AddOrUpgradeResult(results, new ConflictSearchResult
            {
                ConflictSearchId = conflictSearchId,
                MatterId = candidate.MatterId,
                ClientId = candidate.ClientId,
                SearchTerm = term,
                MatchedName = candidate.MatchedName,
                MatchedOn = candidate.MatchedOn,
                MatchType = candidate.MatchType,
                PartyRole = candidate.PartyRole,
                Score = score
            });
        }
    }

    private static ConflictCandidate ScoreCandidate(
        string term,
        string normalizedTerm,
        string matchedName,
        string normalizedCandidate,
        string matchedOn,
        string matchType)
    {
        var recalculatedCandidate = NormalizeName(matchedName);
        normalizedCandidate = string.IsNullOrWhiteSpace(recalculatedCandidate)
            ? normalizedCandidate
            : recalculatedCandidate;

        var score = CalculateScore(normalizedTerm, normalizedCandidate);
        if (score >= 100)
        {
            matchType = "Exact normalized match";
        }
        else if (CanUsePhraseContainment(normalizedTerm, normalizedCandidate) && score >= 88)
        {
            matchType = "Strong phrase match";
        }
        else if (score >= 80)
        {
            matchType = "Strong fuzzy match";
        }
        else if (score >= 60)
        {
            matchType = "Token/contains match";
        }

        return new ConflictCandidate(term, matchedName, matchedOn, matchType, score);
    }

    private static int CalculateScore(string normalizedTerm, string normalizedCandidate)
    {
        if (string.IsNullOrWhiteSpace(normalizedTerm) || string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return 0;
        }

        var directScore = CalculateScoreCore(normalizedTerm, normalizedCandidate);
        var connectorInsensitiveTerm = RemoveConnectorWords(normalizedTerm);
        var connectorInsensitiveCandidate = RemoveConnectorWords(normalizedCandidate);

        if (connectorInsensitiveTerm == normalizedTerm && connectorInsensitiveCandidate == normalizedCandidate)
        {
            return directScore;
        }

        return Math.Max(directScore, CalculateScoreCore(connectorInsensitiveTerm, connectorInsensitiveCandidate));
    }

    private static int CalculateScoreCore(string normalizedTerm, string normalizedCandidate)
    {
        if (string.IsNullOrWhiteSpace(normalizedTerm) || string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return 0;
        }

        if (normalizedTerm == normalizedCandidate)
        {
            return 100;
        }

        var termTokens = normalizedTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidateTokens = normalizedCandidate.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var overlap = termTokens.Intersect(candidateTokens, StringComparer.OrdinalIgnoreCase).Count();
        var termCoverage = termTokens.Count == 0 ? 0 : (double)overlap / termTokens.Count;
        var candidateCoverage = candidateTokens.Count == 0 ? 0 : (double)overlap / candidateTokens.Count;

        var containsScore = 0;
        if (CanUsePhraseContainment(normalizedTerm, normalizedCandidate))
        {
            var phraseCoverage = Math.Max(termCoverage, candidateCoverage);
            containsScore = 88 + (int)Math.Round(Math.Min(1, phraseCoverage) * 6);
        }

        var prefixScore = CalculateTokenPrefixScore(
            normalizedTerm,
            normalizedCandidate,
            termTokens,
            candidateTokens);
        var tokenScore = (int)Math.Round(((termCoverage * 0.7) + (candidateCoverage * 0.3)) * 92);

        var trigramScore = (int)Math.Round(TrigramSimilarity(normalizedTerm, normalizedCandidate) * 100);
        var editScore = (int)Math.Round(LevenshteinRatio(normalizedTerm, normalizedCandidate) * 100);

        var score = Math.Max(containsScore, Math.Max(prefixScore, Math.Max(tokenScore, Math.Max(trigramScore, editScore))));
        return Math.Clamp(score, 0, 99);
    }

    private static int CalculateTokenPrefixScore(
        string normalizedTerm,
        string normalizedCandidate,
        HashSet<string> termTokens,
        HashSet<string> candidateTokens)
    {
        if (termTokens.Count == 0 || candidateTokens.Count == 0)
        {
            return 0;
        }

        var searchableTermTokens = termTokens
            .Where(token => token.Length >= 4)
            .ToList();
        if (searchableTermTokens.Count == 0)
        {
            return 0;
        }

        var matchedTerms = searchableTermTokens.Count(termToken =>
            candidateTokens.Any(candidateToken => candidateToken.StartsWith(termToken, StringComparison.OrdinalIgnoreCase)));
        if (matchedTerms == 0)
        {
            return 0;
        }

        var termCoverage = (double)matchedTerms / searchableTermTokens.Count;
        var compactTermLength = CompactLength(normalizedTerm);
        var compactCandidateLength = CompactLength(normalizedCandidate);
        var lengthPenalty = compactCandidateLength == 0
            ? 0
            : Math.Min(1, (double)compactTermLength / compactCandidateLength);

        return 78 + (int)Math.Round(termCoverage * 14) + (int)Math.Round(lengthPenalty * 4);
    }

    private static string RemoveConnectorWords(string value)
    {
        return string.Join(' ', SplitNormalizedTokens(value).Where(x => !ConnectorWords.Contains(x)));
    }

    private static bool IsPhraseContainment(string normalizedTerm, string normalizedCandidate)
    {
        var termTokens = SplitNormalizedTokens(normalizedTerm);
        var candidateTokens = SplitNormalizedTokens(normalizedCandidate);

        return ContainsTokenPhrase(candidateTokens, termTokens) ||
            ContainsTokenPhrase(termTokens, candidateTokens);
    }

    private static bool CanUsePhraseContainment(string normalizedTerm, string normalizedCandidate)
    {
        return CompactLength(normalizedTerm) >= 3 &&
            CompactLength(normalizedCandidate) >= 3 &&
            IsPhraseContainment(normalizedTerm, normalizedCandidate);
    }

    private static int CompactLength(string value)
    {
        return value.Count(char.IsLetterOrDigit);
    }

    private static List<string> SplitNormalizedTokens(string value)
    {
        return value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static bool ContainsTokenPhrase(IReadOnlyList<string> haystack, IReadOnlyList<string> needle)
    {
        if (needle.Count == 0 || haystack.Count < needle.Count)
        {
            return false;
        }

        for (var start = 0; start <= haystack.Count - needle.Count; start++)
        {
            var matches = true;
            for (var offset = 0; offset < needle.Count; offset++)
            {
                if (!haystack[start + offset].Equals(needle[offset], StringComparison.OrdinalIgnoreCase))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private static double TrigramSimilarity(string left, string right)
    {
        var leftTrigrams = Trigrams(left);
        var rightTrigrams = Trigrams(right);
        if (leftTrigrams.Count == 0 || rightTrigrams.Count == 0)
        {
            return 0;
        }

        var intersection = leftTrigrams.Intersect(rightTrigrams).Count();
        var union = leftTrigrams.Union(rightTrigrams).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static HashSet<string> Trigrams(string value)
    {
        value = $"  {value}  ";
        var trigrams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < value.Length - 2; i++)
        {
            trigrams.Add(value.Substring(i, 3));
        }

        return trigrams;
    }

    private static double LevenshteinRatio(string left, string right)
    {
        var distance = LevenshteinDistance(left, right);
        var maxLength = Math.Max(left.Length, right.Length);
        return maxLength == 0 ? 1 : 1 - (double)distance / maxLength;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var matrix = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i++)
        {
            matrix[i, 0] = i;
        }

        for (var j = 0; j <= right.Length; j++)
        {
            matrix[0, j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[left.Length, right.Length];
    }

    private static void AddPartyResults(
        Dictionary<string, ConflictSearchResult> results,
        Guid conflictSearchId,
        string searchTerm,
        Party party,
        int score,
        string matchedName,
        string matchedOn,
        string matchType,
        string? relationshipType)
    {
        if (party.MatterParties.Count == 0)
        {
            AddOrUpgradeResult(results, new ConflictSearchResult
            {
                ConflictSearchId = conflictSearchId,
                PartyId = party.Id,
                SearchTerm = searchTerm,
                MatchedName = matchedName,
                MatchedOn = relationshipType is null ? matchedOn : $"{matchedOn}: {relationshipType}",
                MatchType = matchType,
                PartyRole = "Global party",
                Score = score
            });
            return;
        }

        foreach (var matterParty in party.MatterParties)
        {
            AddOrUpgradeResult(results, new ConflictSearchResult
            {
                ConflictSearchId = conflictSearchId,
                PartyId = party.Id,
                MatterId = matterParty.MatterId,
                ClientId = matterParty.Matter?.ClientId,
                SearchTerm = searchTerm,
                MatchedName = matchedName,
                MatchedOn = relationshipType is null ? matchedOn : $"{matchedOn}: {relationshipType}",
                MatchType = matchType,
                PartyRole = matterParty.Role,
                Score = score
            });
        }
    }

    private static void AddClientResults(
        Dictionary<string, ConflictSearchResult> results,
        Guid conflictSearchId,
        string searchTerm,
        Client client,
        int score,
        string matchedName,
        string matchedOn,
        string matchType)
    {
        if (client.Matters.Count == 0)
        {
            AddOrUpgradeResult(results, new ConflictSearchResult
            {
                ConflictSearchId = conflictSearchId,
                ClientId = client.Id,
                SearchTerm = searchTerm,
                MatchedName = matchedName,
                MatchedOn = matchedOn,
                MatchType = matchType,
                PartyRole = "Client",
                Score = score
            });
            return;
        }

        foreach (var matter in client.Matters)
        {
            AddOrUpgradeResult(results, new ConflictSearchResult
            {
                ConflictSearchId = conflictSearchId,
                MatterId = matter.Id,
                ClientId = client.Id,
                SearchTerm = searchTerm,
                MatchedName = matchedName,
                MatchedOn = matchedOn,
                MatchType = matchType,
                PartyRole = "Client",
                Score = score
            });
        }
    }

    private static void AddMatterResult(
        Dictionary<string, ConflictSearchResult> results,
        Guid conflictSearchId,
        string searchTerm,
        Matter matter,
        int score,
        string matchedName,
        string matchedOn,
        string matchType)
    {
        AddOrUpgradeResult(results, new ConflictSearchResult
        {
            ConflictSearchId = conflictSearchId,
            MatterId = matter.Id,
            ClientId = matter.ClientId,
            SearchTerm = searchTerm,
            MatchedName = matchedName,
            MatchedOn = matchedOn,
            MatchType = matchType,
            PartyRole = "Matter",
            Score = score
        });
    }

    private static void AddOrUpgradeResult(Dictionary<string, ConflictSearchResult> results, ConflictSearchResult result)
    {
        var sourceKey = result.PartyId?.ToString() ?? $"{result.MatchType}:{result.MatchedOn}:{result.MatchedName}";
        var key = $"{sourceKey}:{result.MatterId}:{result.ClientId}:{result.SearchTerm}:{result.PartyRole}";
        if (!results.TryGetValue(key, out var existing) || result.Score > existing.Score)
        {
            results[key] = result;
        }
    }

    private static string DetermineRiskLevel(ConflictSearchResult result)
    {
        var isAdverse = result.PartyRole is PartyRoles.AdverseParty or PartyRoles.OpposingCounsel;
        var hasLinkedContext = result.MatterId.HasValue || result.ClientId.HasValue;
        var isRelationship = result.MatchType.Contains("Relationship", StringComparison.OrdinalIgnoreCase) ||
            result.MatchedOn.Contains("relationship", StringComparison.OrdinalIgnoreCase);
        var isPriorHistory = result.PartyRole.StartsWith("Prior ", StringComparison.OrdinalIgnoreCase);

        var contextualBoost = 0;
        contextualBoost += isAdverse ? 15 : 0;
        contextualBoost += hasLinkedContext ? 6 : 0;
        contextualBoost += isRelationship ? 6 : 0;
        contextualBoost += isPriorHistory ? 4 : 0;
        var contextualRiskScore = result.Score + contextualBoost;

        if (result.Score >= 100 || (isAdverse && result.Score >= 80) || (contextualBoost > 0 && contextualRiskScore >= 98))
        {
            return ConflictRiskLevels.Critical;
        }

        if (result.Score >= 90 || contextualRiskScore >= 85)
        {
            return ConflictRiskLevels.High;
        }

        if (result.Score >= 65 || contextualRiskScore >= 65 || isRelationship || isPriorHistory)
        {
            return ConflictRiskLevels.Medium;
        }

        return ConflictRiskLevels.Low;
    }

    private static string BuildExplanation(ConflictSearchResult result)
    {
        if (result.PartyId is null && result.PartyRole.StartsWith("Prior ", StringComparison.OrdinalIgnoreCase))
        {
            var contextText = result.MatterId.HasValue ? " with linked matter context" : string.Empty;
            return $"{result.MatchType} for \"{result.SearchTerm}\" in \"{result.MatchedName}\"{contextText}. Match strength: {result.Score}/100.";
        }

        var matterText = result.MatterId.HasValue ? " on a linked matter" : string.Empty;
        return $"{result.MatchType} for \"{result.SearchTerm}\" against \"{result.MatchedName}\"{matterText}. Role: {result.PartyRole}. Match strength: {result.Score}/100.";
    }

    private static string BuildAiAssessment(ConflictSearchResult result)
    {
        var action = result.RiskLevel switch
        {
            ConflictRiskLevels.Critical => "Treat as a must-review hit before approval.",
            ConflictRiskLevels.High => "Review closely and compare party role, matter context, and relationship history.",
            ConflictRiskLevels.Medium => "Likely worth a conflicts reviewer look, especially if parties are related.",
            _ => "Low-confidence candidate; useful mainly as a safety net."
        };

        return $"AI assist: {action} The hit came from {result.MatchedOn.ToLowerInvariant()} with {result.Score}/100 match strength.";
    }

    private static string BuildAiSummary(ConflictSearch search, List<string> terms)
    {
        if (search.Results.Count == 0)
        {
            return $"AI assist: No candidate hits were found for {terms.Count} search term(s). A human reviewer should still confirm the party list is complete.";
        }

        var highestRisk = search.Results
            .OrderByDescending(x => Array.IndexOf(ConflictRiskLevels.All, x.RiskLevel))
            .ThenByDescending(x => x.Score)
            .First();

        var criticalCount = search.Results.Count(x => x.RiskLevel == ConflictRiskLevels.Critical);
        var highCount = search.Results.Count(x => x.RiskLevel == ConflictRiskLevels.High);

        return $"AI assist: Found {search.Results.Count} candidate hit(s) across {terms.Count} term(s). Highest risk is {highestRisk.RiskLevel} for {highestRisk.MatchedName}. Critical: {criticalCount}; High: {highCount}.";
    }

    private static string JoinSearchableText(params string?[] values)
    {
        return string.Join(' ', values.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private sealed record HistoricalConflictText(
        Guid? MatterId,
        Guid? ClientId,
        string MatchedName,
        string MatchedOn,
        string MatchType,
        string PartyRole,
        string SearchableText);

    private sealed record ConflictCandidate(
        string SearchTerm,
        string MatchedName,
        string MatchedOn,
        string MatchType,
        int Score);

    private sealed record AzureSearchRequest(
        [property: JsonPropertyName("search")] string Search,
        [property: JsonPropertyName("top")] int Top,
        [property: JsonPropertyName("select")] string Select,
        [property: JsonPropertyName("searchFields")] string SearchFields,
        [property: JsonPropertyName("searchMode")] string SearchMode,
        [property: JsonPropertyName("queryType")] string QueryType,
        [property: JsonPropertyName("filter")] string? Filter,
        [property: JsonPropertyName("orderby")] string OrderBy);

    private sealed record AzureSearchResponse(
        [property: JsonPropertyName("value")] IReadOnlyList<AzureSearchDocument> Value);

    private sealed record AzureSearchDocument(
        [property: JsonPropertyName("id")] string Id);
}

public sealed record ConflictPreview(
    IReadOnlyList<string> Terms,
    string Summary,
    IReadOnlyList<ConflictPreviewItem> Results,
    int TotalResults,
    int CriticalCount,
    int HighCount,
    int RelationshipCount,
    int PriorSearchCount);

public sealed record ConflictPreviewItem(
    string SearchTerm,
    string MatchedName,
    string MatchedOn,
    string MatchType,
    string PartyRole,
    int Score,
    string RiskLevel,
    string Explanation,
    string AiAssessment,
    Guid? PartyId,
    Guid? MatterId,
    int? MatterNumber,
    string MatterName,
    Guid? ClientId,
    int? ClientNumber,
    string ClientName);
