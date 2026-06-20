namespace CMIForge.Services;

public class GraphMailOptions
{
    public bool Enabled { get; set; } = true;

    public string TenantId { get; set; } = string.Empty;

    public string ManagedIdentityClientId { get; set; } = string.Empty;
}
