namespace CMIForge.Models;

public class ConflictSearchDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string SourceType { get; set; } = string.Empty;

    public Guid SourceId { get; set; }

    public Guid? PartyId { get; set; }

    public Guid? MatterId { get; set; }

    public Guid? ClientId { get; set; }

    public string MatchedName { get; set; } = string.Empty;

    public string MatchedOn { get; set; } = string.Empty;

    public string MatchType { get; set; } = string.Empty;

    public string PartyRole { get; set; } = string.Empty;

    public string SearchableText { get; set; } = string.Empty;

    public string NormalizedSearchableText { get; set; } = string.Empty;

    public int SortNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class ConflictSearchDocumentSourceTypes
{
    public const string Party = "Party";
    public const string PartyAlias = "PartyAlias";
    public const string Client = "Client";
    public const string ClientAlias = "ClientAlias";
    public const string Matter = "Matter";
    public const string PriorSearch = "PriorSearch";
    public const string PriorResult = "PriorResult";
    public const string ArchivedResult = "ArchivedResult";
}
