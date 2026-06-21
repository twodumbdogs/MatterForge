namespace CMIForge.Services;

public static class FormFieldRules
{
    public static bool IsVisible(FormField field, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(field.VisibleWhenFieldKey) || string.IsNullOrWhiteSpace(field.VisibleWhenValue))
        {
            return true;
        }

        if (!values.TryGetValue(field.VisibleWhenFieldKey, out var actual))
        {
            return false;
        }

        return NormalizeConditionValue(actual).Equals(NormalizeConditionValue(field.VisibleWhenValue), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsEditableForWorkflowStep(FormField field, IReadOnlySet<string> openStepNames)
    {
        return string.IsNullOrWhiteSpace(field.EditableStepName) ||
            openStepNames.Contains(field.EditableStepName.Trim());
    }

    private static string NormalizeConditionValue(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized switch
        {
            "on" => "true",
            "yes" => "true",
            "Yes" => "true",
            "True" => "true",
            "false" => "false",
            "False" => "false",
            _ => normalized
        };
    }
}
