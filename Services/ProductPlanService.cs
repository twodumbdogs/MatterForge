using MatterForge.Data;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Services;

public static class ProductInfo
{
    public const string Name = "CMIForge";
    public const string Version = "20260618.1";
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

    public static IReadOnlySet<string> All { get; } = Labels.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

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
    int? ClientLimit,
    IReadOnlySet<string> FeatureKeys,
    string? Note = null)
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
    private static readonly HashSet<string> AllFeatureKeys = new(StringComparer.OrdinalIgnoreCase)
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
    };

    private static readonly ProductPlan Community = new(
        "community",
        "Community",
        "Free",
        UserLimit: 3,
        MatterLimit: 50,
        ClientLimit: 50,
        FeatureKeys: AllFeatureKeys,
        Note: "Free tier with community support.");

    private static readonly ProductPlan Professional = new(
        "professional",
        "Professional",
        "$149/month",
        UserLimit: 10,
        MatterLimit: 500,
        ClientLimit: 500,
        FeatureKeys: AllFeatureKeys,
        Note: "Email support and room for the core intake team.");

    private static readonly ProductPlan Enterprise = new(
        "enterprise",
        "Enterprise",
        "Coming soon",
        UserLimit: null,
        MatterLimit: null,
        ClientLimit: null,
        FeatureKeys: AllFeatureKeys,
        Note: "Enterprise packaging is coming soon.");

    public IReadOnlyList<ProductPlan> Plans { get; } = [Community, Professional, Enterprise];

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

    public async Task<ProductLimitStatus> GetClientLimitAsync()
    {
        var count = await db.Clients.CountAsync();
        return BuildLimitStatus("clients", count, CurrentPlan.ClientLimit);
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
