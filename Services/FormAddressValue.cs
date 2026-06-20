using Microsoft.AspNetCore.Http;

namespace CMIForge.Services;

public static class FormAddressValue
{
    public static AddressParts FromForm(IFormCollection form, string key)
    {
        return new AddressParts(
            Get(form, key, "AddressLine1"),
            Get(form, key, "AddressLine2"),
            Get(form, key, "City"),
            Get(form, key, "State"),
            Get(form, key, "PostalCode"),
            Get(form, key, "Country"));
    }

    public static AddressParts Parse(string? value)
    {
        var parts = (value ?? string.Empty)
            .Split('\n', StringSplitOptions.TrimEntries)
            .ToList();

        while (parts.Count < 4)
        {
            parts.Add(string.Empty);
        }

        var cityStatePostal = parts.Count > 2 ? parts[2] : string.Empty;
        var city = string.Empty;
        var state = string.Empty;
        var postalCode = string.Empty;
        if (!string.IsNullOrWhiteSpace(cityStatePostal))
        {
            var cityAndRest = cityStatePostal.Split(',', 2, StringSplitOptions.TrimEntries);
            city = cityAndRest[0];
            if (cityAndRest.Length > 1)
            {
                var statePostal = cityAndRest[1].Split(' ', 2, StringSplitOptions.TrimEntries);
                state = statePostal[0];
                postalCode = statePostal.Length > 1 ? statePostal[1] : string.Empty;
            }
        }

        return new AddressParts(
            parts[0],
            parts[1],
            city,
            state,
            postalCode,
            parts.Count > 3 ? parts[3] : string.Empty);
    }

    public static string Compose(AddressParts parts)
    {
        var cityLine = string.Join(", ", new[] { parts.City, parts.State }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (!string.IsNullOrWhiteSpace(parts.PostalCode))
        {
            cityLine = string.IsNullOrWhiteSpace(cityLine)
                ? parts.PostalCode.Trim()
                : $"{cityLine} {parts.PostalCode.Trim()}";
        }

        return string.Join(
            Environment.NewLine,
            new[] { parts.AddressLine1, parts.AddressLine2, cityLine, parts.Country }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()));
    }

    private static string Get(IFormCollection form, string key, string part)
    {
        return form[$"Fields[{key}].{part}"].ToString().Trim();
    }
}

public sealed record AddressParts(
    string AddressLine1,
    string AddressLine2,
    string City,
    string State,
    string PostalCode,
    string Country);
