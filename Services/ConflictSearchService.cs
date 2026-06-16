using System.Globalization;
using System.Text;
using MatterForge.Data;
using MatterForge.Models;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Services;

public class ConflictSearchService(MatterForgeDbContext db)
{
    private static readonly HashSet<string> CorporateSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the",
        "co", "company", "corp", "corporation",
        "inc", "incorporated",
        "llc", "llp", "lp", "ltd", "limited",
        "plc", "pllc", "pc", "pa",
        "holdings", "holding", "group"
    };

    public static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var chars = decomposed
            .Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark)
            .Select(x => char.IsLetterOrDigit(x) ? x : ' ')
            .ToArray();

        var tokens = new string(chars)
            .Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !CorporateSuffixes.Contains(x))
            .ToList();

        return string.Join(' ', tokens);
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
            SearchName = string.IsNullOrWhiteSpace(searchName) ? $"Conflict Search {nextSearchNumber:D8}" : searchName.Trim(),
            SearchTerms = searchTerms.Trim(),
            FormSubmissionId = formSubmissionId,
            MatterId = matterId,
            RequestedByUserId = requestedByUserId
        };

        db.ConflictSearches.Add(search);
        await RunSearchAsync(search);
        await db.SaveChangesAsync();
        return search;
    }

    public async Task RunSearchAsync(ConflictSearch search)
    {
        var terms = SplitSearchTerms(search.SearchTerms);
        search.NormalizedTerms = string.Join(Environment.NewLine, terms.Select(NormalizeName));
        search.Status = ConflictSearchStatuses.PendingReview;
        search.ReviewerDecision = ConflictSearchDecisions.Pending;
        search.UpdatedAt = DateTimeOffset.UtcNow;

        if (search.Id != Guid.Empty)
        {
            var existingResults = await db.ConflictSearchResults
                .Where(x => x.ConflictSearchId == search.Id)
                .ToListAsync();
            db.ConflictSearchResults.RemoveRange(existingResults);
        }

        var parties = await db.Parties
            .Include(x => x.Aliases)
            .Include(x => x.MatterParties)
                .ThenInclude(x => x.Matter)
                    .ThenInclude(x => x!.Client)
            .OrderBy(x => x.PartyNumber)
            .ToListAsync();
        var partiesById = parties.ToDictionary(x => x.Id);

        var relationships = await db.PartyRelationships
            .Include(x => x.FromParty)
            .Include(x => x.ToParty)
            .ToListAsync();

        var results = new Dictionary<string, ConflictSearchResult>();

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
        }

        search.Results.Clear();
        foreach (var result in results.Values.OrderByDescending(x => x.Score).ThenBy(x => x.MatchedName))
        {
            result.RiskLevel = DetermineRiskLevel(result.Score, result.PartyRole, result.MatchType);
            result.Explanation = BuildExplanation(result);
            result.AiAssessment = BuildAiAssessment(result);
            search.Results.Add(result);
        }

        search.AiSummary = BuildAiSummary(search, terms);
    }

    public async Task ApplyReviewDecisionAsync(Guid searchId, string decision, string notes, Guid? reviewedByUserId)
    {
        var search = await db.ConflictSearches.FirstOrDefaultAsync(x => x.Id == searchId);
        if (search is null)
        {
            return;
        }

        search.ReviewerDecision = ConflictSearchDecisions.All.Contains(decision) ? decision : ConflictSearchDecisions.Pending;
        search.Status = search.ReviewerDecision switch
        {
            ConflictSearchDecisions.Clear => ConflictSearchStatuses.Cleared,
            ConflictSearchDecisions.Conflict => ConflictSearchStatuses.Conflict,
            ConflictSearchDecisions.PotentialConflict => ConflictSearchStatuses.PotentialConflict,
            ConflictSearchDecisions.NeedsInfo => ConflictSearchStatuses.NeedsInfo,
            _ => ConflictSearchStatuses.PendingReview
        };
        search.ReviewNotes = notes?.Trim() ?? string.Empty;
        search.ReviewedByUserId = reviewedByUserId;
        search.ReviewedAt = DateTimeOffset.UtcNow;
        search.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
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

    private static ConflictCandidate ScoreCandidate(
        string term,
        string normalizedTerm,
        string matchedName,
        string normalizedCandidate,
        string matchedOn,
        string matchType)
    {
        normalizedCandidate = string.IsNullOrWhiteSpace(normalizedCandidate)
            ? NormalizeName(matchedName)
            : normalizedCandidate;

        var score = CalculateScore(normalizedTerm, normalizedCandidate);
        if (score >= 98)
        {
            matchType = "Exact normalized match";
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

        if (normalizedTerm == normalizedCandidate)
        {
            return 100;
        }

        if (normalizedCandidate.Contains(normalizedTerm, StringComparison.OrdinalIgnoreCase) ||
            normalizedTerm.Contains(normalizedCandidate, StringComparison.OrdinalIgnoreCase))
        {
            return 85;
        }

        var termTokens = normalizedTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidateTokens = normalizedCandidate.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var overlap = termTokens.Intersect(candidateTokens, StringComparer.OrdinalIgnoreCase).Count();
        var tokenScore = termTokens.Count == 0 ? 0 : (int)Math.Round((double)overlap / termTokens.Count * 80);

        var trigramScore = (int)Math.Round(TrigramSimilarity(normalizedTerm, normalizedCandidate) * 100);
        var editScore = (int)Math.Round(LevenshteinRatio(normalizedTerm, normalizedCandidate) * 100);

        return Math.Max(tokenScore, Math.Max(trigramScore, editScore));
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

    private static void AddOrUpgradeResult(Dictionary<string, ConflictSearchResult> results, ConflictSearchResult result)
    {
        var key = $"{result.PartyId}:{result.MatterId}:{result.SearchTerm}:{result.PartyRole}";
        if (!results.TryGetValue(key, out var existing) || result.Score > existing.Score)
        {
            results[key] = result;
        }
    }

    private static string DetermineRiskLevel(int score, string partyRole, string matchType)
    {
        var isAdverse = partyRole is PartyRoles.AdverseParty or PartyRoles.OpposingCounsel;
        if ((isAdverse && score >= 80) || score >= 98)
        {
            return ConflictRiskLevels.Critical;
        }

        if (score >= 85 || (isAdverse && score >= 65))
        {
            return ConflictRiskLevels.High;
        }

        if (score >= 65 || matchType.Contains("Relationship", StringComparison.OrdinalIgnoreCase))
        {
            return ConflictRiskLevels.Medium;
        }

        return ConflictRiskLevels.Low;
    }

    private static string BuildExplanation(ConflictSearchResult result)
    {
        var matterText = result.MatterId.HasValue ? " on a linked matter" : string.Empty;
        return $"{result.MatchType} for \"{result.SearchTerm}\" against \"{result.MatchedName}\"{matterText}. Role: {result.PartyRole}. Score: {result.Score}.";
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

        return $"AI assist: {action} The hit came from {result.MatchedOn.ToLowerInvariant()} with a {result.Score}% match score.";
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

    private sealed record ConflictCandidate(
        string SearchTerm,
        string MatchedName,
        string MatchedOn,
        string MatchType,
        int Score);
}
