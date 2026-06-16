using System.Text.Json;

namespace MatterForge.Services;

public static class FormJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static FormSchema DeserializeSchema(string schemaJson)
    {
        return JsonSerializer.Deserialize<FormSchema>(schemaJson, Options) ?? new FormSchema();
    }
}
