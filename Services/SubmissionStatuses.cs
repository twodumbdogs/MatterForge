namespace MatterForge.Services;

public static class SubmissionStatuses
{
    public const string Submitted = "Submitted";
    public const string InReview = "In Review";
    public const string Approved = "Approved";
    public const string Returned = "Returned";
    public const string Converted = "Converted";

    public static readonly string[] All =
    [
        Submitted,
        InReview,
        Approved,
        Returned,
        Converted
    ];

    public static bool IsValid(string? status)
    {
        return All.Contains(status, StringComparer.OrdinalIgnoreCase);
    }
}
