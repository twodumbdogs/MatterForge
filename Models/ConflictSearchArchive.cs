namespace CMIForge.Models;

public class ConflictSearchArchive
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConflictSearchId { get; set; }

    public ConflictSearch? ConflictSearch { get; set; }

    public int SearchNumber { get; set; }

    public string SearchName { get; set; } = string.Empty;

    public string SearchTerms { get; set; } = string.Empty;

    public string NormalizedTerms { get; set; } = string.Empty;

    public string SearchableText { get; set; } = string.Empty;

    public string NormalizedSearchableText { get; set; } = string.Empty;

    public Guid? FormSubmissionId { get; set; }

    public FormSubmission? FormSubmission { get; set; }

    public int? SubmissionNumber { get; set; }

    public string FormName { get; set; } = string.Empty;

    public Guid? MatterId { get; set; }

    public Matter? Matter { get; set; }

    public int? MatterNumber { get; set; }

    public string MatterName { get; set; } = string.Empty;

    public Guid? ClientId { get; set; }

    public Client? Client { get; set; }

    public int? ClientNumber { get; set; }

    public string ClientName { get; set; } = string.Empty;

    public Guid? RequestedByUserId { get; set; }

    public CMIForgeUser? RequestedByUser { get; set; }

    public string RequestedByDisplayName { get; set; } = string.Empty;

    public Guid? ReviewedByUserId { get; set; }

    public CMIForgeUser? ReviewedByUser { get; set; }

    public string ReviewedByDisplayName { get; set; } = string.Empty;

    public string Status { get; set; } = ConflictSearchStatuses.Cleared;

    public string ReviewerDecision { get; set; } = ConflictSearchDecisions.Clear;

    public string ReviewNotes { get; set; } = string.Empty;

    public string AiSummary { get; set; } = string.Empty;

    public int ResultCount { get; set; }

    public string HighestRiskLevel { get; set; } = ConflictRiskLevels.Low;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public DateTimeOffset ArchivedAt { get; set; } = DateTimeOffset.UtcNow;

    public string PayloadCompression { get; set; } = ConflictArchiveCompression.Gzip;

    public byte[] PayloadBytes { get; set; } = [];

    public List<ConflictSearchHitArchive> Hits { get; set; } = [];
}

public class ConflictSearchHitArchive
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConflictSearchArchiveId { get; set; }

    public ConflictSearchArchive? ConflictSearchArchive { get; set; }

    public Guid ConflictSearchId { get; set; }

    public Guid ConflictSearchResultId { get; set; }

    public int SearchNumber { get; set; }

    public string SearchTerm { get; set; } = string.Empty;

    public string MatchedName { get; set; } = string.Empty;

    public string MatchedOn { get; set; } = string.Empty;

    public string MatchType { get; set; } = string.Empty;

    public string PartyRole { get; set; } = string.Empty;

    public Guid? PartyId { get; set; }

    public Party? Party { get; set; }

    public string PartyName { get; set; } = string.Empty;

    public Guid? MatterId { get; set; }

    public Matter? Matter { get; set; }

    public int? MatterNumber { get; set; }

    public string MatterName { get; set; } = string.Empty;

    public Guid? ClientId { get; set; }

    public Client? Client { get; set; }

    public int? ClientNumber { get; set; }

    public string ClientName { get; set; } = string.Empty;

    public int Score { get; set; }

    public string RiskLevel { get; set; } = ConflictRiskLevels.Low;

    public string Explanation { get; set; } = string.Empty;

    public string AiAssessment { get; set; } = string.Empty;

    public string ClearanceStatus { get; set; } = ConflictSearchDecisions.Clear;

    public string ClearanceNotes { get; set; } = string.Empty;

    public Guid? ClearedByUserId { get; set; }

    public CMIForgeUser? ClearedByUser { get; set; }

    public string ClearedByDisplayName { get; set; } = string.Empty;

    public Guid? ClearedAsUserId { get; set; }

    public CMIForgeUser? ClearedAsUser { get; set; }

    public string ClearedAsDisplayName { get; set; } = string.Empty;

    public DateTimeOffset? ClearedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string SearchableText { get; set; } = string.Empty;

    public string NormalizedSearchableText { get; set; } = string.Empty;
}

public static class ConflictArchiveCompression
{
    public const string Gzip = "gzip";
}
