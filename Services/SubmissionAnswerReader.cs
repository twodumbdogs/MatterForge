using System.Text.Json;

namespace CMIForge.Services;

public static class SubmissionAnswerReader
{
    public static Dictionary<string, string> Read(string dataJson)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson, FormJson.Options) ?? [];

        return values.ToDictionary(
            x => NormalizeKey(x.Key),
            x => FormatValue(x.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    public static string FirstValue(Dictionary<string, string> answers, params string[] keys)
    {
        foreach (var key in keys.Select(NormalizeKey))
        {
            if (answers.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    public static string NormalizeKey(string key)
    {
        return new string(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    public static string FormatValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True => "Yes",
            JsonValueKind.False => "No",
            JsonValueKind.Null => string.Empty,
            _ => value.ToString()
        };
    }
}
