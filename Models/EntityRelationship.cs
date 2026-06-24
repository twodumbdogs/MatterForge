namespace CMIForge.Models;

public class EntityRelationship
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FromEntityType { get; set; } = EntityRelationshipEntityTypes.Client;

    public Guid FromEntityId { get; set; }

    public string ToEntityType { get; set; } = EntityRelationshipEntityTypes.Party;

    public Guid ToEntityId { get; set; }

    public Guid RelationshipTypeId { get; set; }

    public RelationshipType? RelationshipType { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class EntityRelationshipEntityTypes
{
    public const string Client = "Client";
    public const string Matter = "Matter";
    public const string Party = "Party";
    public const string Contact = "Contact";
    public const string User = "User";

    public static readonly string[] Entities = [Client, Matter, Party, Contact];
    public static readonly string[] All = [Client, Matter, Party, Contact, User];

    public static bool IsUser(string value)
    {
        return string.Equals(value, User, StringComparison.OrdinalIgnoreCase);
    }
}
