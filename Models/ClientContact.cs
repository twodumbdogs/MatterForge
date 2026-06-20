namespace CMIForge.Models;

public class ClientContact
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }

    public Client? Client { get; set; }

    public Guid ContactId { get; set; }

    public Contact? Contact { get; set; }

    public string Role { get; set; } = ContactRoles.Primary;

    public bool IsPrimary { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
