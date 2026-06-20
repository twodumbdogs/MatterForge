namespace CMIForge.Services;

public static class ProtectedSystemUsers
{
    public const int RootUserSystemId = 1;

    public const string RootUserMessage =
        "User 00000001 is a protected system user and cannot be edited in any environment.";

    public static bool IsProtected(int systemId)
    {
        return systemId == RootUserSystemId;
    }
}
