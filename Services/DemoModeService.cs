namespace MatterForge.Services;

public class DemoModeService(IConfiguration configuration)
{
    public const int ProtectedSystemUserId = 1;

    public bool IsEnabled => configuration.GetValue<bool>("MatterForge:DemoMode");

    public bool ScheduledResetEnabled => configuration.GetValue("MatterForge:DemoResetEnabled", true);

    public double ResetIntervalHours => Math.Max(1, configuration.GetValue("MatterForge:DemoResetIntervalHours", 12.0));

    public string BannerMessage =>
        $"Public demo mode is active. Demo data resets every {ResetIntervalHours:g} hours, abusive content is blocked, and admin/destructive changes are disabled.";

    public string BlockedActionMessage =>
        "That action is disabled in the public demo so the demo stays clean for everyone.";

    public string BlockedContentMessage =>
        "The public demo blocks abusive, unsafe, or brand-damaging content. Please edit the text and try again.";

    public bool IsProtectedSystemUser(int systemId)
    {
        return IsEnabled && systemId == ProtectedSystemUserId;
    }

    public string ProtectedUserMessage =>
        "User 00000001 is protected in the live demo so visitors cannot change the demo administrator.";

    public bool AllowsManualReset(PathString path, string? handlerName)
    {
        return IsEnabled &&
            path.StartsWithSegments("/System/Demo") &&
            handlerName?.Equals("OnPostResetAsync", StringComparison.OrdinalIgnoreCase) == true;
    }

    public bool ShouldBlockPost(PathString path, string? handlerName)
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (AllowsManualReset(path, handlerName))
        {
            return false;
        }

        if (ContainsDestructiveHandler(handlerName))
        {
            return true;
        }

        return path.StartsWithSegments("/Security") ||
            path.StartsWithSegments("/System/Settings") ||
            path.StartsWithSegments("/Imports") ||
            path.StartsWithSegments("/Forms/Create") ||
            path.StartsWithSegments("/Forms/Edit") ||
            path.StartsWithSegments("/Workflow/Definitions");
    }

    private static bool ContainsDestructiveHandler(string? handlerName)
    {
        if (string.IsNullOrWhiteSpace(handlerName))
        {
            return false;
        }

        return handlerName.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
            handlerName.Contains("Remove", StringComparison.OrdinalIgnoreCase);
    }
}
