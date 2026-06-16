using MatterForge.Data;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Services;

public static class ProductInfo
{
    public const string Name = "CMIForge";
    public const string Version = "20260616.1";
}

public static class ProductFeatureKeys
{
    public const string BasicConflicts = "basic-conflicts";
    public const string BasicIntakeForms = "basic-intake-forms";
    public const string Workflow = "workflow";
    public const string EmailNotifications = "email-notifications";
    public const string AuditTrail = "audit-trail";
    public const string AdvancedWorkflow = "advanced-workflow";
    public const string Reporting = "reporting";
    public const string TimeRecording = "time-recording";
    public const string EntraSso = "entra-sso";
    public const string FutureFancy = "future-fancy";

    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        [BasicConflicts] = "Basic conflicts search",
        [BasicIntakeForms] = "Basic intake forms",
        [Workflow] = "Workflow",
        [EmailNotifications] = "Email notifications",
        [AuditTrail] = "Audit trail",
        [AdvancedWorkflow] = "Advanced workflow",
        [Reporting] = "Reporting",
        [TimeRecording] = "Time recording",
        [EntraSso] = "Azure AD / SSO",
        [FutureFancy] = "Future fancy stuff"
    };

    public static string Label(string featureKey)
    {
        return Labels.TryGetValue(featureKey, out var label) ? label : featureKey;
    }
}

public sealed record ProductPlan(
    string Key,
    string Name,
    string PriceRange,
    int? UserLimit,
    int? MatterLimit,
    bool UnlimitedClients,
    IReadOnlySet<string> FeatureKeys)
{
    public bool Allows(string featureKey)
    {
        return FeatureKeys.Contains(featureKey);
    }
}

public sealed record ProductUsageSnapshot(int UserCount, int MatterCount, int ClientCount);

public sealed record ProductLimitStatus(
    string ResourceName,
    int CurrentCount,
    int? Limit,
    bool CanCreate,
    string Message);

public class ProductPlanService(MatterForgeDbContext db)
{
    private static readonly ProductPlan Free = new(
        "free",
        "Free",
        "$0/mo",
        UserLimit: 3,
        MatterLimit: 100,
        UnlimitedClients: true,
        FeatureKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ProductFeatureKeys.BasicConflicts,
            ProductFeatureKeys.BasicIntakeForms
        });

    private static readonly ProductPlan Standard = new(
        "standard",
        "Standard",
        "$99/mo",
        UserLimit: 10,
        MatterLimit: null,
        UnlimitedClients: true,
        FeatureKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ProductFeatureKeys.BasicConflicts,
            ProductFeatureKeys.BasicIntakeForms,
            ProductFeatureKeys.Workflow,
            ProductFeatureKeys.EmailNotifications,
            ProductFeatureKeys.AuditTrail,
            ProductFeatureKeys.TimeRecording
        });

    private static readonly ProductPlan Professional = new(
        "professional",
        "Professional",
        "$299/mo",
        UserLimit: null,
        MatterLimit: null,
        UnlimitedClients: true,
        FeatureKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ProductFeatureKeys.BasicConflicts,
            ProductFeatureKeys.BasicIntakeForms,
            ProductFeatureKeys.Workflow,
            ProductFeatureKeys.EmailNotifications,
            ProductFeatureKeys.AuditTrail,
            ProductFeatureKeys.AdvancedWorkflow,
            ProductFeatureKeys.Reporting,
            ProductFeatureKeys.TimeRecording,
            ProductFeatureKeys.EntraSso,
            ProductFeatureKeys.FutureFancy
        });

    public IReadOnlyList<ProductPlan> Plans { get; } = [Free, Standard, Professional];

    public ProductPlan CurrentPlan => Professional;

    public bool AllowsFeature(string featureKey)
    {
        return CurrentPlan.Allows(featureKey);
    }

    public async Task<ProductUsageSnapshot> GetUsageAsync()
    {
        return new ProductUsageSnapshot(
            await db.Users.CountAsync(),
            await db.Matters.CountAsync(),
            await db.Clients.CountAsync());
    }

    public async Task<ProductLimitStatus> GetUserLimitAsync()
    {
        var count = await db.Users.CountAsync();
        return BuildLimitStatus("users", count, CurrentPlan.UserLimit);
    }

    public async Task<ProductLimitStatus> GetMatterLimitAsync()
    {
        var count = await db.Matters.CountAsync();
        return BuildLimitStatus("matters", count, CurrentPlan.MatterLimit);
    }

    private static ProductLimitStatus BuildLimitStatus(string resourceName, int currentCount, int? limit)
    {
        if (!limit.HasValue)
        {
            return new ProductLimitStatus(resourceName, currentCount, null, true, $"Unlimited {resourceName} on this plan.");
        }

        var canCreate = currentCount < limit.Value;
        var message = canCreate
            ? $"{currentCount:N0} of {limit.Value:N0} {resourceName} used."
            : $"The current plan is limited to {limit.Value:N0} {resourceName}. Upgrade to add more.";

        return new ProductLimitStatus(resourceName, currentCount, limit, canCreate, message);
    }
}
