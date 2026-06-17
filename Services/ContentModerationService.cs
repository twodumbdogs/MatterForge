using System.Text.RegularExpressions;

namespace MatterForge.Services;

public class ContentModerationService
{
    private static readonly Regex[] BlockedTextPatterns =
    [
        new(@"\bf+u+c+k+(?:e+d+|i+n+g+)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bs+h+i+t+(?:t+y+)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bb+i+t+c+h+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\ba+s+s+h+o+l+e+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bb+a+s+t+a+r+d+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bc+u+n+t+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bd+i+c+k+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bp+r+i+c+k+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bw+h+o+r+e+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bs+l+u+t+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bn+a+z+i+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bh+i+t+l+e+r+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bk+i+l+l+\s+y+o+u+r+s+e+l+f+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    ];

    private static readonly string[] BlockedUnsafeFragments =
    [
        "<script",
        "</script",
        "javascript:",
        "data:text/html",
        "onerror=",
        "onclick=",
        "onload="
    ];

    private static readonly HashSet<string> IgnoredFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "__RequestVerificationToken",
        "id",
        "handler",
        "returnUrl"
    };

    public ContentModerationResult ValidateForm(IFormCollection form)
    {
        foreach (var field in form)
        {
            if (ShouldSkipField(field.Key))
            {
                continue;
            }

            foreach (var value in field.Value)
            {
                if (IsBlocked(value))
                {
                    return new ContentModerationResult(false);
                }
            }
        }

        return new ContentModerationResult(true);
    }

    private static bool ShouldSkipField(string fieldName)
    {
        return IgnoredFieldNames.Contains(fieldName) ||
            fieldName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
            fieldName.EndsWith("Ids", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlocked(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var lowered = value.ToLowerInvariant();
        if (BlockedUnsafeFragments.Any(lowered.Contains))
        {
            return true;
        }

        return BlockedTextPatterns.Any(pattern => pattern.IsMatch(value));
    }
}

public sealed record ContentModerationResult(bool IsAllowed);
