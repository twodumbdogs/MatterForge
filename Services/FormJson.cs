using System.Text.Json;

namespace CMIForge.Services;

public static class FormJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static FormSchema DeserializeSchema(string schemaJson)
    {
        var schema = JsonSerializer.Deserialize<FormSchema>(schemaJson, Options) ?? new FormSchema();
        schema.Normalize();
        return schema;
    }
}
