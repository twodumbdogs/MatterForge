namespace CMIForge.Models;

public class ClientAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }

    public Client? Client { get; set; }

    public string Alias { get; set; } = string.Empty;

    public string NormalizedAlias { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
