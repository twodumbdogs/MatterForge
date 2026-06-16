using System.Text.Json.Serialization;

namespace MatterForge.Services;

public class FormSchema
{
    public string Title { get; set; } = string.Empty;

    public List<FormField> Fields { get; set; } = [];
}

public class FormField
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter<FieldType>))]
    public FieldType Type { get; set; } = FieldType.Text;

    public bool Required { get; set; }

    public List<string> Options { get; set; } = [];
}

public enum FieldType
{
    Text,
    TextArea,
    Date,
    Currency,
    Number,
    Client,
    Select,
    Checkbox
}
