namespace CMIForge.Models;

public class EnhancementRequestVote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EnhancementRequestId { get; set; }

    public EnhancementRequest? EnhancementRequest { get; set; }

    public Guid UserId { get; set; }

    public CMIForgeUser? User { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
