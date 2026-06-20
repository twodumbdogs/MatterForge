namespace CMIForge.Models;

public class SystemSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Key { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string ValueType { get; set; } = SystemSettingValueTypes.Text;

    public bool IsSecret { get; set; }

    public bool IsEditable { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public CMIForgeUser? UpdatedByUser { get; set; }
}

public static class SystemSettingValueTypes
{
    public const string Text = "Text";
    public const string Integer = "Integer";
    public const string Boolean = "Boolean";
    public const string Email = "Email";
    public const string SecretReference = "SecretReference";
}
