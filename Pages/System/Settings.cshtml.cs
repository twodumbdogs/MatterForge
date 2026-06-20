using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.System;

public class SettingsModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    CurrentUserService currentUserService,
    DemoModeService demoModeService,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public List<SettingInput> Settings { get; set; } = [];

    public bool IsDemoMode => demoModeService.IsEnabled;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        await LoadSettingsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        if (IsDemoMode)
        {
            await LoadSettingsAsync();
            ModelState.AddModelError(string.Empty, "System settings are read-only in demo mode.");
            return Page();
        }

        var settings = await db.SystemSettings
            .OrderBy(x => x.Category)
            .ThenBy(x => x.DisplayName)
            .ToListAsync();
        ModelState.Clear();

        foreach (var setting in settings)
        {
            var input = Settings.FirstOrDefault(x => x.Id == setting.Id);
            if (input is null || !setting.IsEditable)
            {
                continue;
            }

            ValidateSetting(setting, input);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        var changedKeys = new List<string>();

        foreach (var setting in settings)
        {
            var input = Settings.FirstOrDefault(x => x.Id == setting.Id);
            if (input is null || !setting.IsEditable)
            {
                continue;
            }

            if (setting.IsSecret && string.IsNullOrWhiteSpace(input.Value))
            {
                continue;
            }

            var nextValue = NormalizeValue(setting, input.Value);
            if (setting.Value == nextValue)
            {
                continue;
            }

            setting.Value = nextValue;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
            setting.UpdatedByUserId = currentUser?.Id;
            changedKeys.Add(setting.Key);
        }

        if (changedKeys.Count > 0)
        {
            await db.SaveChangesAsync();
            await auditLogService.LogAsync(
                "SystemSettings.Updated",
                "SystemSetting",
                summary: $"Updated {changedKeys.Count} system setting(s).",
                details: new { SettingKeys = changedKeys });
            TempData["StatusMessage"] = $"Updated {changedKeys.Count} setting{(changedKeys.Count == 1 ? string.Empty : "s")}.";
        }
        else
        {
            TempData["StatusMessage"] = "No settings changed.";
        }

        return RedirectToPage();
    }

    private async Task LoadSettingsAsync()
    {
        await EnsureMissingSettingsAsync();

        Settings = await db.SystemSettings
            .AsNoTracking()
            .Where(x => !x.Key.StartsWith("Email.Smtp"))
            .OrderBy(x => x.Category)
            .ThenBy(x => x.DisplayName)
            .Select(x => new SettingInput
            {
                Id = x.Id,
                Key = x.Key,
                Category = x.Category,
                DisplayName = x.DisplayName,
                Description = x.Description,
                Value = x.IsSecret ? string.Empty : x.Value,
                ValueType = x.ValueType,
                IsSecret = x.IsSecret,
                IsEditable = x.IsEditable,
                HasValue = !string.IsNullOrWhiteSpace(x.Value),
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync();
    }

    private async Task EnsureMissingSettingsAsync()
    {
        var defaults = new[]
        {
            new SettingDefault("Conflicts.LivePreviewEnabled", "Conflicts", "Live conflict preview", "Shows the conflict radar while users type client, matter, contact, or party names on intake forms.", "true", SystemSettingValueTypes.Boolean),
            new SettingDefault("AddressLookup.Enabled", "Address Lookup", "Enable address lookup", "Turns address autocomplete suggestions on for client and contact address fields when a provider key is configured.", "false", SystemSettingValueTypes.Boolean),
            new SettingDefault("AddressLookup.GeoapifyApiKey", "Address Lookup", "Geoapify API key", "Server-side Geoapify key used for address autocomplete. The key is never sent to browsers.", string.Empty, SystemSettingValueTypes.SecretReference, IsSecret: true),
            new SettingDefault("AddressLookup.CountryFilter", "Address Lookup", "Country filter", "Optional ISO country code used to narrow address suggestions, such as us. Leave blank for worldwide lookup.", "us", SystemSettingValueTypes.Text),
            new SettingDefault("AddressLookup.ResultLimit", "Address Lookup", "Suggestion limit", "Maximum address suggestions returned while a user types.", "5", SystemSettingValueTypes.Integer),
            new SettingDefault("Email.NotificationsEnabled", "Email", "Enable email notifications", "Turns outbound workflow and system email notifications on or off.", "false", SystemSettingValueTypes.Boolean),
            new SettingDefault("Email.MailboxAddress", "Email", "Mailbox anchor", "Shared mailbox CMIForge uses through Microsoft Graph when sending this tenant's outbound mail.", "intake@cmiforge.com", SystemSettingValueTypes.Email),
            new SettingDefault("Email.FromEmail", "Email", "From email", "Tenant sender address used on outbound CMIForge notifications.", "customer0@cmiforge.com", SystemSettingValueTypes.Email),
            new SettingDefault("Email.ReplyToEmail", "Email", "Reply-to email", "Tenant reply address used on outbound CMIForge notifications.", "customer0@cmiforge.com", SystemSettingValueTypes.Email),
            new SettingDefault("Email.FromName", "Email", "From name", "Display name used as the sender for outbound CMIForge notifications.", "Customer 0", SystemSettingValueTypes.Text)
        };

        var changed = false;
        foreach (var item in defaults)
        {
            var setting = await db.SystemSettings.FirstOrDefaultAsync(x => x.Key == item.Key);
            if (setting is null)
            {
                setting = new SystemSetting
                {
                    Key = item.Key,
                    Value = item.Value
                };
                db.SystemSettings.Add(setting);
            }

            if (setting.Category != item.Category ||
                setting.DisplayName != item.DisplayName ||
                setting.Description != item.Description ||
                setting.ValueType != item.ValueType ||
                setting.IsSecret != item.IsSecret)
            {
                setting.Category = item.Category;
                setting.DisplayName = item.DisplayName;
                setting.Description = item.Description;
                setting.ValueType = item.ValueType;
                setting.IsSecret = item.IsSecret;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }

    private void ValidateSetting(SystemSetting setting, SettingInput input)
    {
        if (setting.IsSecret && string.IsNullOrWhiteSpace(input.Value))
        {
            return;
        }

        var value = input.Value?.Trim() ?? string.Empty;
        switch (setting.ValueType)
        {
            case SystemSettingValueTypes.Integer:
                if (!int.TryParse(value, out var intValue) || intValue < 0)
                {
                    ModelState.AddModelError($"Settings[{Settings.IndexOf(input)}].Value", $"{setting.DisplayName} must be a whole number.");
                }
                break;
            case SystemSettingValueTypes.Boolean:
                if (!bool.TryParse(value, out _))
                {
                    ModelState.AddModelError($"Settings[{Settings.IndexOf(input)}].Value", $"{setting.DisplayName} must be true or false.");
                }
                break;
            case SystemSettingValueTypes.Email:
                if (!string.IsNullOrWhiteSpace(value) && !IsValidEmail(value))
                {
                    ModelState.AddModelError($"Settings[{Settings.IndexOf(input)}].Value", $"{setting.DisplayName} must be a valid email address.");
                }
                break;
        }
    }

    private static string NormalizeValue(SystemSetting setting, string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return setting.ValueType switch
        {
            SystemSettingValueTypes.Boolean when bool.TryParse(trimmed, out var parsed) => parsed ? "true" : "false",
            _ => trimmed
        };
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            _ = new MailAddress(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public class SettingInput
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Display(Name = "Value")]
    public string? Value { get; set; }

    public string ValueType { get; set; } = string.Empty;

    public bool IsSecret { get; set; }

    public bool IsEditable { get; set; }

    public bool HasValue { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed record SettingDefault(
    string Key,
    string Category,
    string DisplayName,
    string Description,
    string Value,
    string ValueType,
    bool IsSecret = false);
