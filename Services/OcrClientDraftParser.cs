using System.Text.RegularExpressions;

namespace MatterForge.Services;

public sealed record OcrClientDraft(
    string Name,
    string PrimaryContact,
    string Email,
    string Phone,
    string AddressLine1,
    string AddressLine2,
    string City,
    string State,
    string PostalCode,
    string Country);

public static class OcrClientDraftParser
{
    private static readonly Regex EmailRegex = new(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"(?:\+?1[\s.-]?)?(?:\(?\d{3}\)?[\s.-]?)\d{3}[\s.-]?\d{4}", RegexOptions.Compiled);
    private static readonly Regex CityStateZipRegex = new(@"^(?<city>[A-Za-z .'-]+),?\s+(?<state>[A-Z]{2})\s+(?<zip>\d{5}(?:-\d{4})?)$", RegexOptions.Compiled);
    private static readonly Regex StreetRegex = new(@"\d+.+\b(?:Street|St\.?|Avenue|Ave\.?|Road|Rd\.?|Boulevard|Blvd\.?|Drive|Dr\.?|Lane|Ln\.?|Court|Ct\.?|Circle|Cir\.?|Way|Parkway|Pkwy\.?|Place|Pl\.?|Suite|Ste\.?|Floor|Fl\.?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static OcrClientDraft Parse(string? rawText)
    {
        var lines = NormalizeLines(rawText);
        var email = MatchValue(lines, EmailRegex);
        var phone = MatchValue(lines, PhoneRegex);
        var cityStateZip = lines
            .Select(line => CityStateZipRegex.Match(line))
            .FirstOrDefault(match => match.Success);
        var addressLine1 = lines.FirstOrDefault(line => StreetRegex.IsMatch(line)) ?? string.Empty;
        var addressLine2 = lines
            .SkipWhile(line => !string.Equals(line, addressLine1, StringComparison.Ordinal))
            .Skip(1)
            .FirstOrDefault(IsAddressContinuation) ?? string.Empty;
        var primaryContact = FindPrimaryContact(lines);
        var name = FindClientName(lines, email, phone, addressLine1, addressLine2, primaryContact);

        return new OcrClientDraft(
            name,
            primaryContact,
            email,
            phone,
            addressLine1,
            addressLine2,
            cityStateZip?.Groups["city"].Value.Trim() ?? string.Empty,
            cityStateZip?.Groups["state"].Value.Trim() ?? string.Empty,
            cityStateZip?.Groups["zip"].Value.Trim() ?? string.Empty,
            FindCountry(lines));
    }

    private static List<string> NormalizeLines(string? rawText)
    {
        return (rawText ?? string.Empty)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim(' ', '|', '-', '_'))
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .ToList();
    }

    private static string MatchValue(IEnumerable<string> lines, Regex regex)
    {
        return lines
            .Select(line => regex.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Value.Trim())
            .FirstOrDefault() ?? string.Empty;
    }

    private static string FindPrimaryContact(IReadOnlyList<string> lines)
    {
        var labeled = lines
            .Select(line => Regex.Match(line, @"^(?:attn|attention|contact)\s*:?\s*(?<name>.+)$", RegexOptions.IgnoreCase))
            .FirstOrDefault(match => match.Success);

        if (labeled is { Success: true })
        {
            return labeled.Groups["name"].Value.Trim();
        }

        return lines.FirstOrDefault(line =>
            line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length is >= 2 and <= 4 &&
            !EmailRegex.IsMatch(line) &&
            !PhoneRegex.IsMatch(line) &&
            !StreetRegex.IsMatch(line) &&
            !CityStateZipRegex.IsMatch(line) &&
            !LooksLikeOrganization(line)) ?? string.Empty;
    }

    private static string FindClientName(
        IReadOnlyList<string> lines,
        string email,
        string phone,
        string addressLine1,
        string addressLine2,
        string primaryContact)
    {
        return lines.FirstOrDefault(line =>
            !string.Equals(line, email, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(line, phone, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(line, addressLine1, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(line, addressLine2, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(line, primaryContact, StringComparison.OrdinalIgnoreCase) &&
            !EmailRegex.IsMatch(line) &&
            !PhoneRegex.IsMatch(line) &&
            !StreetRegex.IsMatch(line) &&
            !CityStateZipRegex.IsMatch(line) &&
            !LooksLikeWebOrNoise(line)) ?? string.Empty;
    }

    private static bool IsAddressContinuation(string line)
    {
        return Regex.IsMatch(line, @"\b(?:Suite|Ste\.?|Unit|Apt\.?|Floor|Fl\.?|PO Box|P\.O\. Box)\b", RegexOptions.IgnoreCase);
    }

    private static bool LooksLikeOrganization(string line)
    {
        return Regex.IsMatch(line, @"\b(?:LLC|L\.L\.C\.|Inc\.?|Corporation|Corp\.?|Company|Co\.?|LLP|PLC|PC|P\.C\.|Group|Holdings|Partners)\b", RegexOptions.IgnoreCase);
    }

    private static bool LooksLikeWebOrNoise(string line)
    {
        return line.Contains("www.", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("http", StringComparison.OrdinalIgnoreCase) ||
            line.Contains(".com", StringComparison.OrdinalIgnoreCase) ||
            line.Equals("invoice", StringComparison.OrdinalIgnoreCase) ||
            line.Equals("bill to", StringComparison.OrdinalIgnoreCase) ||
            line.Length < 3;
    }

    private static string FindCountry(IEnumerable<string> lines)
    {
        return lines.FirstOrDefault(line =>
            line.Equals("United States", StringComparison.OrdinalIgnoreCase) ||
            line.Equals("USA", StringComparison.OrdinalIgnoreCase) ||
            line.Equals("U.S.A.", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
    }
}
