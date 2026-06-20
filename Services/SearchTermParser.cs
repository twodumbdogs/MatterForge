namespace CMIForge.Services;

public static class SearchTermParser
{
    public static int? TryParseRecordNumber(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var digits = new string(search.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        return int.TryParse(digits, out var number) ? number : null;
    }
}
