namespace CMIForge.Models;

public class PartyAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PartyId { get; set; }

    public Party? Party { get; set; }

    public string Alias { get; set; } = string.Empty;

    public string NormalizedAlias { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
