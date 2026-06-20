namespace CMIForge.Models;

public class CMIForgeUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int SystemId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string MiddleName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string EntraTenantId { get; set; } = string.Empty;

    public string EntraObjectId { get; set; } = string.Empty;

    public string EntraUserPrincipalName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsArchived { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastLoginAt { get; set; }

    public List<Matter> ResponsibleMatters { get; set; } = [];

    public List<Matter> LeadPartnerMatters { get; set; } = [];

    public List<TimeEntry> TimeEntries { get; set; } = [];

    public List<TeamMember> TeamMemberships { get; set; } = [];

    public List<UserRole> Roles { get; set; } = [];
}
