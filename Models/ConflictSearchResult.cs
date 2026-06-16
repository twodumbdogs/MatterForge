namespace MatterForge.Models;

public class ConflictSearchResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConflictSearchId { get; set; }

    public ConflictSearch? ConflictSearch { get; set; }

    public Guid? PartyId { get; set; }

    public Party? Party { get; set; }

    public Guid? MatterId { get; set; }

    public Matter? Matter { get; set; }

    public Guid? ClientId { get; set; }

    public Client? Client { get; set; }

    public string SearchTerm { get; set; } = string.Empty;

    public string MatchedName { get; set; } = string.Empty;

    public string MatchedOn { get; set; } = string.Empty;

    public string MatchType { get; set; } = string.Empty;

    public string PartyRole { get; set; } = string.Empty;

    public int Score { get; set; }

    public string RiskLevel { get; set; } = ConflictRiskLevels.Low;

    public string Explanation { get; set; } = string.Empty;

    public string AiAssessment { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class ConflictRiskLevels
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";

    public static readonly string[] All = [Low, Medium, High, Critical];
}
