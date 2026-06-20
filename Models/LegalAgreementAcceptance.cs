namespace CMIForge.Models;

public class LegalAgreementAcceptance
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantProvisioningRequestId { get; set; }

    public TenantProvisioningRequest? TenantProvisioningRequest { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string AcceptedByName { get; set; } = string.Empty;

    public string AcceptedByEmail { get; set; } = string.Empty;

    public string AgreementKey { get; set; } = LegalAgreementVersions.CurrentKey;

    public string AgreementVersion { get; set; } = LegalAgreementVersions.CurrentVersion;

    public string AgreementTitle { get; set; } = LegalAgreementVersions.CurrentTitle;

    public string ProductVersion { get; set; } = string.Empty;

    public bool Accepted { get; set; } = true;

    public DateTimeOffset AcceptedAt { get; set; } = DateTimeOffset.UtcNow;

    public string IpAddress { get; set; } = string.Empty;

    public string UserAgent { get; set; } = string.Empty;
}

public static class LegalAgreementVersions
{
    public const string CurrentKey = "cmiforge-saas-license-terms";
    public const string CurrentVersion = "2026-06-19";
    public const string CurrentTitle = "CMIForge SaaS Terms, Legal Use, and License Agreement";
}
