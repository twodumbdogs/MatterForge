namespace MatterForge.Services;

public class DemoModeService(IConfiguration configuration)
{
    public const int ProtectedSystemUserId = 1;

    public bool IsEnabled => configuration.GetValue<bool>("MatterForge:DemoMode");

    public bool IsProtectedSystemUser(int systemId)
    {
        return IsEnabled && systemId == ProtectedSystemUserId;
    }

    public string ProtectedUserMessage =>
        "User 00000001 is protected in the live demo so visitors cannot change the demo administrator.";
}
