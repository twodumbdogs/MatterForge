namespace MatterForge.Services;

public class UserDateTimeService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
{
    private const string TimeZoneCookieName = "cmiforge.timezone";

    public string Format(DateTimeOffset value, string format = "g")
    {
        return TimeZoneInfo.ConvertTime(value, GetUserTimeZone()).ToString(format);
    }

    public string FormatNullable(DateTimeOffset? value, string format = "g", string fallback = "")
    {
        return value.HasValue ? Format(value.Value, format) : fallback;
    }

    public TimeZoneInfo GetUserTimeZone()
    {
        var timeZoneId = httpContextAccessor.HttpContext?.Request.Cookies[TimeZoneCookieName];
        return TryFindTimeZone(timeZoneId) ??
            TryFindTimeZone(configuration["MatterForge:DefaultTimeZone"]) ??
            TimeZoneInfo.Local;
    }

    private static TimeZoneInfo? TryFindTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
