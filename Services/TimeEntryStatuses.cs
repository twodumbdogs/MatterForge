namespace CMIForge.Services;

public static class TimeEntryStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";

    public static readonly IReadOnlyList<string> All =
    [
        Draft,
        Submitted,
        Approved
    ];
}
