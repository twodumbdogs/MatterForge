namespace CMIForge.Models;

public class PartyRelationship
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FromPartyId { get; set; }

    public Party? FromParty { get; set; }

    public Guid ToPartyId { get; set; }

    public Party? ToParty { get; set; }

    public string RelationshipType { get; set; } = PartyRelationshipTypes.Related;

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class PartyRelationshipTypes
{
    public const string Parent = "Parent";
    public const string Subsidiary = "Subsidiary";
    public const string Affiliate = "Affiliate";
    public const string AcquiredBy = "Acquired By";
    public const string Contact = "Contact";
    public const string Related = "Related";

    public static readonly string[] All = [Parent, Subsidiary, Affiliate, AcquiredBy, Contact, Related];
}
