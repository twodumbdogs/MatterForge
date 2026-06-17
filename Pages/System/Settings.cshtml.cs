using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.System;

public class SettingsModel(
    MatterForgeDbContext db,
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
        Settings = await db.SystemSettings
            .AsNoTracking()
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
    public string Value { get; set; } = string.Empty;

    public string ValueType { get; set; } = string.Empty;

    public bool IsSecret { get; set; }

    public bool IsEditable { get; set; }

    public bool HasValue { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
