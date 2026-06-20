namespace CMIForge.Models;

public class MatterContact
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MatterId { get; set; }

    public Matter? Matter { get; set; }

    public Guid ContactId { get; set; }

    public Contact? Contact { get; set; }

    public string Role { get; set; } = ContactRoles.MatterContact;

    public bool IsPrimary { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
