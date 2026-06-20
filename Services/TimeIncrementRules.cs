namespace CMIForge.Services;

public static class TimeIncrementRules
{
    public const int ActualMinutes = 0;
    public const int SixMinutes = 6;
    public const int FifteenMinutes = 15;
    public const string SystemDefaultSettingKey = "Time.DefaultIncrementMinutes";

    public static readonly IReadOnlyList<int> AllowedValues = [ActualMinutes, SixMinutes, FifteenMinutes];

    public static int Normalize(int? value)
    {
        return value.HasValue && AllowedValues.Contains(value.Value)
            ? value.Value
            : ActualMinutes;
    }

    public static int Resolve(int? matterOverride, int systemDefault)
    {
        return matterOverride.HasValue
            ? Normalize(matterOverride)
            : Normalize(systemDefault);
    }

    public static int RoundMinutes(int minutes, int incrementMinutes)
    {
        if (incrementMinutes <= 0)
        {
            return minutes;
        }

        return (int)(Math.Ceiling(minutes / (decimal)incrementMinutes) * incrementMinutes);
    }

    public static string Label(int incrementMinutes)
    {
        return Normalize(incrementMinutes) switch
        {
            SixMinutes => "6-minute increments",
            FifteenMinutes => "15-minute increments",
            _ => "Actual time"
        };
    }
}
