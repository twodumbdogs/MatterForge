namespace CMIForge.Services;

public static class FormSectionSecurity
{
    public static IReadOnlyList<FormSection> VisibleSections(
        FormSchema schema,
        IReadOnlySet<string> userTeamKeys,
        bool canViewRestrictedSections)
    {
        return schema.ResolvedSections
            .Where(section => IsVisible(section, userTeamKeys, canViewRestrictedSections))
            .ToList();
    }

    public static IReadOnlyList<FormSection> PublicSections(FormSchema schema)
    {
        return VisibleSections(schema, new HashSet<string>(StringComparer.OrdinalIgnoreCase), canViewRestrictedSections: false);
    }

    public static bool IsVisible(
        FormSection section,
        IReadOnlySet<string> userTeamKeys,
        bool canViewRestrictedSections)
    {
        if (canViewRestrictedSections || section.VisibleToTeamKeys.Count == 0)
        {
            return true;
        }

        return section.VisibleToTeamKeys.Any(userTeamKeys.Contains);
    }

    public static IReadOnlyList<FormField> VisibleFields(
        FormSchema schema,
        IReadOnlyCollection<FormSection> visibleSections)
    {
        var visibleSectionKeys = visibleSections
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return schema.Fields
            .Where(field =>
            {
                var sectionKey = string.IsNullOrWhiteSpace(field.SectionKey)
                    ? FormSchema.DefaultSectionKey
                    : field.SectionKey;
                return visibleSectionKeys.Contains(sectionKey);
            })
            .ToList();
    }

    public static IReadOnlyList<string> ParseTeamKeys(string? value)
    {
        return (value ?? string.Empty)
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
