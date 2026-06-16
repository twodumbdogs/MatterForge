namespace MatterForge.Services;

public sealed record UserNameParts(string FirstName, string MiddleName, string LastName)
{
    public string DisplayName => BuildDisplayName(FirstName, MiddleName, LastName);

    public static string BuildDisplayName(string firstName, string? middleName, string lastName)
    {
        return string.Join(' ', new[] { firstName, middleName, lastName }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim()));
    }

    public static UserNameParts FromDisplayName(string? displayName, string? fallbackEmail = null)
    {
        var fallback = string.IsNullOrWhiteSpace(fallbackEmail)
            ? "User"
            : fallbackEmail.Split('@', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? fallbackEmail;
        var tokens = (string.IsNullOrWhiteSpace(displayName) ? fallback : displayName)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return tokens.Length switch
        {
            0 => new UserNameParts(fallback, string.Empty, string.Empty),
            1 => new UserNameParts(tokens[0], string.Empty, string.Empty),
            2 => new UserNameParts(tokens[0], string.Empty, tokens[1]),
            _ => new UserNameParts(tokens[0], string.Join(' ', tokens[1..^1]), tokens[^1])
        };
    }
}
