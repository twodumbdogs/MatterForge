namespace CMIForge.Models;

public class Party
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int PartyNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string PartyType { get; set; } = PartyTypes.Organization;

    public string Status { get; set; } = "Active";

    public string Notes { get; set; } = string.Empty;

    public bool IsArchived { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<PartyAlias> Aliases { get; set; } = [];

    public List<MatterParty> MatterParties { get; set; } = [];

    public List<PartyRelationship> OutboundRelationships { get; set; } = [];

    public List<PartyRelationship> InboundRelationships { get; set; } = [];
}

public static class PartyTypes
{
    public const string Organization = "Organization";
    public const string Individual = "Individual";
    public const string Government = "Government";
    public const string Other = "Other";

    public static readonly string[] All = [Organization, Individual, Government, Other];
}
