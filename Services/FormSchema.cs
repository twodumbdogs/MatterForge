using System.Text.Json.Serialization;

namespace CMIForge.Services;

public class FormSchema
{
    public const string DefaultSectionKey = "general";

    public string Title { get; set; } = string.Empty;

    public List<FormSection> Sections { get; set; } = [];

    public List<FormField> Fields { get; set; } = [];

    [JsonIgnore]
    public IReadOnlyList<FormSection> ResolvedSections
    {
        get
        {
            var sections = Sections
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Label))
                .DistinctBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var formField in Fields)
            {
                var sectionKey = string.IsNullOrWhiteSpace(formField.SectionKey)
                    ? DefaultSectionKey
                    : formField.SectionKey.Trim();
                if (!sections.Any(x => x.Key.Equals(sectionKey, StringComparison.OrdinalIgnoreCase)))
                {
                    sections.Add(new FormSection
                    {
                        Key = sectionKey,
                        Label = HumanizeSectionKey(sectionKey)
                    });
                }
            }

            return sections.Count == 0
                ? [new FormSection { Key = DefaultSectionKey, Label = "General" }]
                : sections;
        }
    }

    public void Normalize()
    {
        if (Sections.Count == 0)
        {
            Sections.Add(new FormSection { Key = DefaultSectionKey, Label = "General" });
        }

        var normalizedSections = Sections
            .Where(x => !string.IsNullOrWhiteSpace(x.Label))
            .Select(x => new FormSection
            {
                Key = string.IsNullOrWhiteSpace(x.Key) ? FormSection.KeyFromLabel(x.Label) : x.Key.Trim(),
                Label = x.Label.Trim(),
                VisibleToTeamKeys = x.VisibleToTeamKeys
                    .Where(teamKey => !string.IsNullOrWhiteSpace(teamKey))
                    .Select(teamKey => teamKey.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .DistinctBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedSections.Count == 0)
        {
            normalizedSections.Add(new FormSection { Key = DefaultSectionKey, Label = "General" });
        }

        Sections = normalizedSections;
        foreach (var formField in Fields)
        {
            formField.SectionKey = string.IsNullOrWhiteSpace(formField.SectionKey)
                ? Sections[0].Key
                : formField.SectionKey.Trim();
            formField.VisibleWhenFieldKey = formField.VisibleWhenFieldKey?.Trim() ?? string.Empty;
            formField.VisibleWhenValue = formField.VisibleWhenValue?.Trim() ?? string.Empty;
            formField.EditableStepName = formField.EditableStepName?.Trim() ?? string.Empty;
        }
    }

    public IEnumerable<FormField> FieldsForSection(string sectionKey)
    {
        var key = string.IsNullOrWhiteSpace(sectionKey) ? DefaultSectionKey : sectionKey;
        return Fields.Where(x => (string.IsNullOrWhiteSpace(x.SectionKey) ? DefaultSectionKey : x.SectionKey).Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    private static string HumanizeSectionKey(string key)
    {
        var value = string.IsNullOrWhiteSpace(key) ? DefaultSectionKey : key.Trim();
        return string.Join(' ', value.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Trim();
    }
}

public class FormSection
{
    public string Key { get; set; } = FormSchema.DefaultSectionKey;

    public string Label { get; set; } = "General";

    public List<string> VisibleToTeamKeys { get; set; } = [];

    public static string KeyFromLabel(string? label)
    {
        var raw = string.IsNullOrWhiteSpace(label) ? "General" : label.Trim();
        var chars = raw
            .ToLowerInvariant()
            .Select(x => char.IsLetterOrDigit(x) ? x : '-')
            .ToArray();
        var key = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return string.IsNullOrWhiteSpace(key) ? FormSchema.DefaultSectionKey : key;
    }
}

public class FormField
{
    public string SectionKey { get; set; } = FormSchema.DefaultSectionKey;

    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter<FieldType>))]
    public FieldType Type { get; set; } = FieldType.Text;

    public bool Required { get; set; }

    public List<string> Options { get; set; } = [];

    public string VisibleWhenFieldKey { get; set; } = string.Empty;

    public string VisibleWhenValue { get; set; } = string.Empty;

    public string EditableStepName { get; set; } = string.Empty;
}

public enum FieldType
{
    Text,
    TextArea,
    Date,
    Currency,
    Number,
    Client,
    Address,
    Select,
    Checkbox
}
