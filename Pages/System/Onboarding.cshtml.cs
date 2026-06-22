using CMIForge.Data;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.System;

public class OnboardingModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    IConfiguration configuration) : PageModel
{
    public List<OnboardingItem> Items { get; private set; } = [];

    public int CompletedCount => Items.Count(x => x.IsComplete);

    public int PercentComplete => Items.Count == 0 ? 0 : (int)Math.Round(CompletedCount * 100m / Items.Count);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.SecurityManage))
        {
            return Forbid();
        }

        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var entraLoginConfigured = configuration.GetValue<bool>("Authentication:Microsoft:Enabled") &&
            !string.IsNullOrWhiteSpace(configuration["Authentication:Microsoft:TenantId"]) &&
            !string.IsNullOrWhiteSpace(configuration["Authentication:Microsoft:ClientId"]);
        var entraProvisioningConfigured = configuration.GetValue<bool>("EntraProvisioning:Enabled") &&
            !string.IsNullOrWhiteSpace(configuration["EntraProvisioning:Domain"]);
        var attachmentsConfigured =
            !string.IsNullOrWhiteSpace(configuration["SubmissionAttachments:ConnectionString"]) ||
            !string.IsNullOrWhiteSpace(configuration["SubmissionAttachments:ServiceUri"]) ||
            !string.IsNullOrWhiteSpace(configuration["SubmissionAttachments:AccountName"]);

        var supportEmail = await db.SystemSettings
            .Where(x => x.Key == "General.SupportEmail")
            .Select(x => x.Value)
            .FirstOrDefaultAsync();

        var adminRoleId = await db.SecurityRoles
            .Where(x => x.Key == "administrator")
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync();

        var users = await db.Users.CountAsync(x => x.IsActive);
        var adminAssignments = adminRoleId.HasValue
            ? await db.UserRoles.CountAsync(x => x.SecurityRoleId == adminRoleId.Value)
            : 0;
        var teams = await db.Teams.CountAsync(x => x.IsActive);
        var contacts = await db.Contacts.CountAsync();
        var forms = await db.FormDefinitions.CountAsync();
        var publishedForms = await db.FormVersions.CountAsync(x => x.IsPublished);
        var workflows = await db.WorkflowDefinitions.CountAsync(x => x.IsActive);
        var clients = await db.Clients.CountAsync();
        var matters = await db.Matters.CountAsync();
        var parties = await db.Parties.CountAsync();
        var conflicts = await db.ConflictSearches.CountAsync();
        var imports = await db.ImportBatches.CountAsync();
        var timeEntries = await db.TimeEntries.CountAsync();
        var settingsUpdated = await db.SystemSettings.CountAsync(x => x.UpdatedAt != null);

        Items =
        [
            new("Require real sign-in", entraLoginConfigured, "Configure Microsoft Entra login for the customer app.", "/System/Settings"),
            new("Enable Entra user provisioning", entraProvisioningConfigured, "Connect app-created users to Entra UPNs and temporary passwords.", "/Entities/Users/Create"),
            new("Set support contact", !string.IsNullOrWhiteSpace(supportEmail), "Add the support email shown to users and operators.", "/System/Settings"),
            new("Confirm at least one admin", adminAssignments > 0, "Make sure the customer owner has Administrator permissions.", "/Security/Index"),
            new("Add initial users", users > 1, "Create the first working users beyond the protected system account.", "/Entities/Users/Index"),
            new("Create teams", teams > 0, "Build groups for intake review, conflicts, and approval queues.", "/Security/Teams/Index"),
            new("Publish intake forms", forms > 0 && publishedForms > 0, "Create or confirm the forms users will submit.", "/Forms/Index"),
            new("Attach workflows", workflows > 0, "Create approval queues and routing steps behind the forms.", "/Workflow/Definitions/Index"),
            new("Load clients", clients > 0, "Import or create the initial client list.", "/Entities/Clients/Index"),
            new("Load matters", matters > 0, "Import or create matters so context exists for conflicts and time.", "/Entities/Matters/Index"),
            new("Add address-book contacts", contacts > 0, "Create client and matter contacts that are not CMIForge login users.", "/Entities/Contacts/Index"),
            new("Load parties", parties > 0, "Import parties so conflicts searches have useful data.", "/Entities/Parties/Index"),
            new("Run a conflicts test", conflicts > 0, "Run at least one search and verify clearance workflow.", "/Conflicts/Index"),
            new("Exercise imports/exports", imports > 0, "Validate the CSV import path and export download path before the customer depends on them.", "/Imports/Index"),
            new("Record sample time", timeEntries > 0, "Confirm time recording and reports are useful for the customer.", "/Time/Index"),
            new("Configure attachments", attachmentsConfigured, "Confirm blob-backed upload settings before users add real files.", "/System/Settings"),
            new("Review system settings", settingsUpdated > 0, "Touch customer-specific settings so defaults are intentional.", "/System/Settings")
        ];
    }
}

public sealed record OnboardingItem(string Title, bool IsComplete, string Description, string Link);
