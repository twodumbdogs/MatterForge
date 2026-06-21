namespace CMIForge.Services;

public static class EntityComplianceService
{
    public const string ReviewStatus = "Compliance Review";

    public static readonly string[] ClientStatusOptions = [ReviewStatus, "Active", "Prospective", "Inactive"];

    public static readonly string[] MatterStatusOptions = [ReviewStatus, "Open", "Pending", "On Hold", "Closed"];

    public static readonly string[] PartyStatusOptions = [ReviewStatus, "Active", "Inactive"];

    public static bool IsReviewStatus(string? status)
    {
        return string.Equals(status?.Trim(), ReviewStatus, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCompliantStatus(string entityType, string? status)
    {
        var value = status?.Trim();
        return entityType switch
        {
            EntityChangeService.ClientEntityType => string.Equals(value, "Active", StringComparison.OrdinalIgnoreCase),
            EntityChangeService.MatterEntityType => string.Equals(value, "Open", StringComparison.OrdinalIgnoreCase),
            "Party" => string.Equals(value, "Active", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    public static bool MovesFromReviewToCompliant(string entityType, string? currentStatus, string? proposedStatus)
    {
        return IsReviewStatus(currentStatus) && IsCompliantStatus(entityType, proposedStatus);
    }

    public static string DirectCreateMessage(string entityName)
    {
        return $"This {entityName.ToLowerInvariant()} is being created directly instead of from an approved workflow. Keep it in Compliance Review until conflicts/compliance checks are complete.";
    }
}
