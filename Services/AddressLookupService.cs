using System.Text.Json;
using System.Text.Json.Serialization;
using CMIForge.Data;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public class AddressLookupService(HttpClient httpClient, CMIForgeDbContext db)
{
    private const string EnabledKey = "AddressLookup.Enabled";
    private const string ApiKeyKey = "AddressLookup.GeoapifyApiKey";
    private const string CountryFilterKey = "AddressLookup.CountryFilter";
    private const string ResultLimitKey = "AddressLookup.ResultLimit";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AddressLookupResponse> SearchAsync(string? text, CancellationToken cancellationToken)
    {
        var query = text?.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
        {
            return new AddressLookupResponse(false, []);
        }

        var settings = await db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key == EnabledKey || x.Key == ApiKeyKey || x.Key == CountryFilterKey || x.Key == ResultLimitKey)
            .ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);

        var enabled = settings.TryGetValue(EnabledKey, out var enabledValue) &&
            bool.TryParse(enabledValue, out var parsedEnabled) &&
            parsedEnabled;
        if (!enabled)
        {
            return new AddressLookupResponse(false, []);
        }

        var apiKey = settings.GetValueOrDefault(ApiKeyKey)?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AddressLookupResponse(false, []);
        }

        var limit = 5;
        if (int.TryParse(settings.GetValueOrDefault(ResultLimitKey), out var parsedLimit))
        {
            limit = Math.Clamp(parsedLimit, 1, 10);
        }

        var requestUri = BuildRequestUri(query, apiKey, settings.GetValueOrDefault(CountryFilterKey), limit);
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new AddressLookupResponse(true, []);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<GeoapifyAutocompleteResponse>(stream, JsonOptions, cancellationToken);
        var suggestions = payload?.Results?
            .Select(ToSuggestion)
            .Where(x => !string.IsNullOrWhiteSpace(x.Label))
            .DistinctBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList() ?? [];

        return new AddressLookupResponse(true, suggestions);
    }

    private static Uri BuildRequestUri(string query, string apiKey, string? countryFilter, int limit)
    {
        var builder = new UriBuilder("https://api.geoapify.com/v1/geocode/autocomplete");
        var parameters = new List<string>
        {
            $"text={Uri.EscapeDataString(query)}",
            "format=json",
            $"limit={limit}",
            $"apiKey={Uri.EscapeDataString(apiKey)}"
        };

        var countryCode = countryFilter?.Trim().TrimStart().TrimEnd().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            parameters.Add($"filter=countrycode:{Uri.EscapeDataString(countryCode)}");
        }

        builder.Query = string.Join("&", parameters);
        return builder.Uri;
    }

    private static AddressSuggestion ToSuggestion(GeoapifyAddressResult result)
    {
        var line1 = FirstNonEmpty(
            result.AddressLine1,
            JoinParts(" ", result.HouseNumber, result.Street),
            result.Name,
            result.Street);

        return new AddressSuggestion(
            FirstNonEmpty(result.Formatted, line1),
            line1,
            string.Empty,
            result.City ?? string.Empty,
            FirstNonEmpty(result.StateCode, result.State),
            result.Postcode ?? string.Empty,
            result.Country ?? string.Empty);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private static string JoinParts(string separator, params string?[] values)
    {
        return string.Join(separator, values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
    }

    private sealed class GeoapifyAutocompleteResponse
    {
        [JsonPropertyName("results")]
        public List<GeoapifyAddressResult>? Results { get; set; }
    }

    private sealed class GeoapifyAddressResult
    {
        [JsonPropertyName("formatted")]
        public string? Formatted { get; set; }

        [JsonPropertyName("address_line1")]
        public string? AddressLine1 { get; set; }

        [JsonPropertyName("address_line2")]
        public string? AddressLine2 { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("housenumber")]
        public string? HouseNumber { get; set; }

        [JsonPropertyName("street")]
        public string? Street { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("state_code")]
        public string? StateCode { get; set; }

        [JsonPropertyName("postcode")]
        public string? Postcode { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }
    }
}

public sealed record AddressLookupResponse(bool Enabled, List<AddressSuggestion> Results);

public sealed record AddressSuggestion(
    string Label,
    string AddressLine1,
    string AddressLine2,
    string City,
    string State,
    string PostalCode,
    string Country);
