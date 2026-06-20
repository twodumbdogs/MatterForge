namespace CMIForge.Models;

public class ConflictSearch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int SearchNumber { get; set; }

    public string SearchName { get; set; } = string.Empty;

    public string SearchTerms { get; set; } = string.Empty;

    public string NormalizedTerms { get; set; } = string.Empty;

    public Guid? FormSubmissionId { get; set; }

    public FormSubmission? FormSubmission { get; set; }

    public Guid? MatterId { get; set; }

    public Matter? Matter { get; set; }

    public Guid? RequestedByUserId { get; set; }

    public CMIForgeUser? RequestedByUser { get; set; }

    public string Status { get; set; } = ConflictSearchStatuses.PendingReview;

    public string ReviewerDecision { get; set; } = ConflictSearchDecisions.Pending;

    public Guid? ReviewedByUserId { get; set; }

    public CMIForgeUser? ReviewedByUser { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string ReviewNotes { get; set; } = string.Empty;

    public string AiSummary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ArchivedAt { get; set; }

    public List<ConflictSearchResult> Results { get; set; } = [];
}

public static class ConflictSearchStatuses
{
    public const string PendingReview = "Pending Review";
    public const string Cleared = "Cleared";
    public const string NeedsInfo = "Needs Info";
    public const string PotentialConflict = "Potential Conflict";
    public const string Conflict = "Conflict";

    public static readonly string[] All = [PendingReview, Cleared, NeedsInfo, PotentialConflict, Conflict];
}

public static class ConflictSearchDecisions
{
    public const string Pending = "Pending";
    public const string Clear = "Clear";
    public const string PotentialConflict = "Potential Conflict";
    public const string Conflict = "Conflict";
    public const string NeedsInfo = "Needs Info";

    public static readonly string[] All = [Pending, Clear, PotentialConflict, Conflict, NeedsInfo];
}
