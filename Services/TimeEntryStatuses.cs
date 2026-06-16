namespace MatterForge.Services;

public static class TimeEntryStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Billed = "Billed";
    public const string NoCharge = "No Charge";

    public static readonly IReadOnlyList<string> All =
    [
        Draft,
        Submitted,
        Approved,
        Billed,
        NoCharge
    ];
}
