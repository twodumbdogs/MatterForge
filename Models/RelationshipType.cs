namespace CMIForge.Models;

public class RelationshipType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Scope { get; set; } = RelationshipScopes.EntityEntity;

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<EntityRelationship> Relationships { get; set; } = [];
}

public static class RelationshipScopes
{
    public const string EntityEntity = "Entity_Entity";
    public const string EntityUser = "Entity_User";
    public const string UserUser = "User_User";

    public static readonly string[] All = [EntityEntity, EntityUser, UserUser];
}
