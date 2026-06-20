namespace CMIForge.Models;

public class MatterParty
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MatterId { get; set; }

    public Matter? Matter { get; set; }

    public Guid PartyId { get; set; }

    public Party? Party { get; set; }

    public string Role { get; set; } = PartyRoles.Client;

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class PartyRoles
{
    public const string Client = "Client";
    public const string AdverseParty = "Adverse Party";
    public const string RelatedParty = "Related Party";
    public const string Witness = "Witness";
    public const string OpposingCounsel = "Opposing Counsel";
    public const string Affiliate = "Affiliate/Subsidiary";
    public const string Other = "Other";

    public static readonly string[] All =
    [
        Client,
        AdverseParty,
        RelatedParty,
        Witness,
        OpposingCounsel,
        Affiliate,
        Other
    ];
}
