namespace MatterForge.Models;

public class TenantProvisioningRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FirmName { get; set; } = string.Empty;

    public string AdminFirstName { get; set; } = string.Empty;

    public string AdminLastName { get; set; } = string.Empty;

    public string AdminEmail { get; set; } = string.Empty;

    public string DesiredDomain { get; set; } = string.Empty;

    public string DesiredSubdomain { get; set; } = string.Empty;

    public string Plan { get; set; } = TenantProvisioningPlans.Community;

    public string Notes { get; set; } = string.Empty;

    public string Status { get; set; } = TenantProvisioningStatuses.New;

    public string InternalNotes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public MatterForgeUser? UpdatedByUser { get; set; }
}

public static class TenantProvisioningStatuses
{
    public const string New = "New";
    public const string Contacted = "Contacted";
    public const string Provisioning = "Provisioning";
    public const string Ready = "Ready";
    public const string Failed = "Failed";
    public const string Closed = "Closed";
}

public static class TenantProvisioningPlans
{
    public const string Community = "Community";
    public const string Professional = "Professional";
    public const string Enterprise = "Enterprise";
}
