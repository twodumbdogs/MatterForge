namespace CMIForge.Models;

public static class DashboardKeys
{
    public const string Firm = "firm";
    public const string User = "user";
    public const string Partner = "partner";
}

public sealed record DashboardDefinition(string Key, string Name, string Description);
