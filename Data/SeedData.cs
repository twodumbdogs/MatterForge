using System.Text.Json;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MatterForge.Data;

public static class SeedData
{
    private const int DemoUserSystemId = 1;
    private const string DemoUserEmail = "imauser@twodumbdogs.com";
    private const string DemoUserFirstName = "Ima";
    private const string DemoUserLastName = "User";
    private const string DemoUserDisplayName = "Ima User";
    private const string PreviousDemoUserEmail = "ima.user@cmiforge.com";
    private const string LegacyStarterUserEmail = "gwms@twodumbdogs.com";

    public static async Task EnsureSeededAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MatterForgeDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var runMigrationsOnStartup = configuration.GetValue("MatterForge:RunMigrationsOnStartup", false);
        var runSeedDataOnStartup = configuration.GetValue("MatterForge:RunSeedDataOnStartup", false);
        if (db.Database.IsRelational() && runMigrationsOnStartup)
        {
            await db.Database.MigrateAsync();
        }

        if (!runSeedDataOnStartup)
        {
            return;
        }

        var seedSampleData = configuration.GetValue("MatterForge:SeedSampleData", false);
        await EnsureApplicationSeedDataAsync(db, seedSampleData);
    }

    public static async Task EnsureApplicationSeedDataAsync(MatterForgeDbContext db, bool seedSampleData = true)
    {
        if (seedSampleData)
        {
            await EnsureStarterUserAsync(db);
        }

        await EnsureStarterSecurityAsync(db);
        await EnsureStarterSystemSettingsAsync(db);
        await EnsureStarterFormAsync(db, seedSampleData);
        await EnsureStarterWorkflowAsync(db);

        if (seedSampleData)
        {
            await EnsureStarterConflictDataAsync(db);
            await EnsureStarterMarketingDemoDataAsync(db);
            await EnsureStarterTimeEntriesAsync(db);
        }
    }

    private static async Task EnsureStarterUserAsync(MatterForgeDbContext db)
    {
        var protectedUser = await db.Users.FirstOrDefaultAsync(x => x.SystemId == DemoUserSystemId);
        if (protectedUser is null)
        {
            protectedUser = await db.Users.FirstOrDefaultAsync(x => x.Email == LegacyStarterUserEmail);
            if (protectedUser is null)
            {
                protectedUser = new MatterForgeUser
                {
                    SystemId = DemoUserSystemId,
                    FirstName = "User",
                    LastName = "One",
                    DisplayName = "User One",
                    Email = "user.one@cmiforge.com",
                    Title = "Administrator"
                };
                db.Users.Add(protectedUser);
            }
        }

        protectedUser.SystemId = DemoUserSystemId;
        if (string.IsNullOrWhiteSpace(protectedUser.DisplayName))
        {
            protectedUser.FirstName = "User";
            protectedUser.MiddleName = string.Empty;
            protectedUser.LastName = "One";
            protectedUser.DisplayName = UserNameParts.BuildDisplayName(protectedUser.FirstName, protectedUser.MiddleName, protectedUser.LastName);
        }

        protectedUser.Title = "Administrator";
        protectedUser.IsActive = true;

        var demoUser = await FindDemoUserAsync(db);
        if (demoUser is null)
        {
            var databaseMaxSystemId = await db.Users.MaxAsync(x => (int?)x.SystemId) ?? 0;
            var localMaxSystemId = db.Users.Local.Count == 0 ? 0 : db.Users.Local.Max(x => x.SystemId);
            var nextSystemId = Math.Max(databaseMaxSystemId, localMaxSystemId) + 1;
            demoUser = new MatterForgeUser
            {
                SystemId = nextSystemId,
                FirstName = DemoUserFirstName,
                MiddleName = string.Empty,
                LastName = DemoUserLastName,
                DisplayName = DemoUserDisplayName,
                Email = DemoUserEmail,
                Title = "Administrator"
            };
            db.Users.Add(demoUser);
        }

        if (string.IsNullOrWhiteSpace(demoUser.FirstName) && string.IsNullOrWhiteSpace(demoUser.LastName) ||
            demoUser.DisplayName.Equals(DemoUserDisplayName, StringComparison.OrdinalIgnoreCase))
        {
            demoUser.FirstName = DemoUserFirstName;
            demoUser.MiddleName = string.Empty;
            demoUser.LastName = DemoUserLastName;
            demoUser.DisplayName = DemoUserDisplayName;
        }

        if (string.IsNullOrWhiteSpace(demoUser.Email) ||
            demoUser.Email.Equals(PreviousDemoUserEmail, StringComparison.OrdinalIgnoreCase))
        {
            demoUser.Email = DemoUserEmail;
        }

        demoUser.Title = "Administrator";
        demoUser.IsActive = true;

        await EnsureUserNamePartsAsync(db);

        await db.SaveChangesAsync();
    }

    private static async Task EnsureStarterSecurityAsync(MatterForgeDbContext db)
    {
        var protectedUser = await db.Users.FirstOrDefaultAsync(x => x.SystemId == DemoUserSystemId);
        var demoUser = await FindDemoUserAsync(db);

        var permissions = new (string Key, string Name, string Category, string Description)[]
        {
            (PermissionKeys.SystemAdmin, "System administrator", "System", "Full CMIForge platform access."),
            (PermissionKeys.FormsView, "View forms", "Forms", "View available form definitions."),
            (PermissionKeys.FormsSubmit, "Submit forms", "Forms", "Submit available intake forms."),
            (PermissionKeys.FormsDesign, "Design forms", "Forms", "Create and publish form versions."),
            (PermissionKeys.SubmissionsViewOwn, "View own submissions", "Submissions", "View submissions submitted by the user."),
            (PermissionKeys.SubmissionsViewAll, "View all submissions", "Submissions", "View all submitted intake records."),
            (PermissionKeys.SubmissionsApprove, "Approve submissions", "Submissions", "Approve or return workflow tasks."),
            (PermissionKeys.SubmissionsConvert, "Convert submissions", "Submissions", "Create client and matter records from submissions."),
            (PermissionKeys.WorkflowsViewQueue, "View workflow queue", "Workflows", "View assigned workflow tasks."),
            (PermissionKeys.WorkflowsViewAllQueues, "View all workflow queues", "Workflows", "View all open workflow tasks."),
            (PermissionKeys.WorkflowsDesign, "Design workflows", "Workflows", "Create and edit workflow definitions and steps."),
            (PermissionKeys.EntitiesView, "View entities", "Entities", "View clients, matters, and users."),
            (PermissionKeys.EntitiesCreate, "Create entities", "Entities", "Create clients, matters, and users."),
            (PermissionKeys.EntitiesEdit, "Submit entity changes", "Entities", "Submit proposed changes to clients, matters, and users."),
            (PermissionKeys.EntitiesApprove, "Approve entity changes", "Entities", "Approve or reject proposed changes to clients and matters."),
            (PermissionKeys.ConflictsView, "View conflict searches", "Conflicts", "View conflict search requests and results."),
            (PermissionKeys.ConflictsRun, "Run conflict searches", "Conflicts", "Create party-based conflict search requests."),
            (PermissionKeys.ConflictsReview, "Review conflict searches", "Conflicts", "Record clearance, potential conflict, conflict, or needs-info decisions."),
            (PermissionKeys.ImportsView, "View imports", "Imports", "View import center and import batch history."),
            (PermissionKeys.ImportsRun, "Run imports", "Imports", "Upload CSV files to import clients, matters, and parties."),
            (PermissionKeys.TimeViewOwn, "View own time", "Time", "View time entries recorded by the user."),
            (PermissionKeys.TimeViewAll, "View all time", "Time", "View time entries across users, clients, and matters."),
            (PermissionKeys.TimeCreate, "Create time", "Time", "Record time entries."),
            (PermissionKeys.TimeEdit, "Edit time", "Time", "Edit existing time entries."),
            (PermissionKeys.TimeApprove, "Approve time", "Time", "Approve submitted time entries."),
            (PermissionKeys.ReportingView, "View reports", "Reporting", "View hardcoded operational reports."),
            (PermissionKeys.SecurityManage, "Manage security", "Security", "Manage teams, roles, and permission assignments.")
        };

        foreach (var permissionSeed in permissions)
        {
            var permission = await db.Permissions.FirstOrDefaultAsync(x => x.Key == permissionSeed.Key);
            if (permission is null)
            {
                permission = new Permission { Key = permissionSeed.Key };
                db.Permissions.Add(permission);
            }

            permission.Name = permissionSeed.Name;
            permission.Category = permissionSeed.Category;
            permission.Description = permissionSeed.Description;
        }

        await db.SaveChangesAsync();

        var administrator = await EnsureRoleAsync(db, "Administrator", "administrator", "Full CMIForge administration.", true);
        var workflowDesigner = await EnsureRoleAsync(db, "Workflow Designer", "workflow-designer", "Can design forms and workflows.", false);
        var intakeReviewer = await EnsureRoleAsync(db, "Intake Reviewer", "intake-reviewer", "Can review and approve assigned intake workflow tasks.", false);
        var entityManager = await EnsureRoleAsync(db, "Entity Manager", "entity-manager", "Can view, create, and edit operational entities.", false);
        var submitter = await EnsureRoleAsync(db, "Submitter", "submitter", "Can submit forms and view their own submissions.", false);

        await EnsureRolePermissionsAsync(db, administrator, permissions.Select(x => x.Key).ToArray());
        await EnsureRolePermissionsAsync(db, workflowDesigner,
            PermissionKeys.FormsView,
            PermissionKeys.FormsSubmit,
            PermissionKeys.FormsDesign,
            PermissionKeys.WorkflowsViewQueue,
            PermissionKeys.WorkflowsDesign,
            PermissionKeys.ReportingView);
        await EnsureRolePermissionsAsync(db, intakeReviewer,
            PermissionKeys.FormsView,
            PermissionKeys.SubmissionsViewAll,
            PermissionKeys.SubmissionsApprove,
            PermissionKeys.WorkflowsViewQueue,
            PermissionKeys.ConflictsView,
            PermissionKeys.ConflictsRun,
            PermissionKeys.ConflictsReview,
            PermissionKeys.TimeViewAll,
            PermissionKeys.ReportingView);
        await EnsureRolePermissionsAsync(db, entityManager,
            PermissionKeys.EntitiesView,
            PermissionKeys.EntitiesCreate,
            PermissionKeys.EntitiesEdit,
            PermissionKeys.EntitiesApprove,
            PermissionKeys.SubmissionsViewAll,
            PermissionKeys.SubmissionsConvert,
            PermissionKeys.ConflictsView,
            PermissionKeys.ConflictsRun,
            PermissionKeys.ImportsView,
            PermissionKeys.ImportsRun,
            PermissionKeys.TimeViewAll,
            PermissionKeys.TimeCreate,
            PermissionKeys.TimeEdit,
            PermissionKeys.ReportingView);
        await EnsureRolePermissionsAsync(db, submitter,
            PermissionKeys.FormsView,
            PermissionKeys.FormsSubmit,
            PermissionKeys.SubmissionsViewOwn,
            PermissionKeys.WorkflowsViewQueue,
            PermissionKeys.TimeViewOwn,
            PermissionKeys.TimeCreate);

        var admins = await EnsureTeamAsync(db, "Admins", "admins", "CMIForge platform administrators.");
        var intakeTeam = await EnsureTeamAsync(db, "Intake Team", "intake-team", "Primary queue for incoming intake review.");
        var cddTeam = await EnsureTeamAsync(db, "CDD Team", "cdd-team", "Client due diligence and risk review.");
        var partnerApprovers = await EnsureTeamAsync(db, "Partner Approvers", "partner-approvers", "Partner-level approval queue.");

        var adminUsers = new[] { protectedUser, demoUser }
            .Where(x => x is not null)
            .Select(x => x!)
            .DistinctBy(x => x.Id)
            .ToList();

        foreach (var adminUser in adminUsers)
        {
            await EnsureTeamMemberAsync(db, admins, adminUser);
            await EnsureTeamMemberAsync(db, intakeTeam, adminUser);
            await EnsureTeamMemberAsync(db, partnerApprovers, adminUser);
            await EnsureUserRoleAsync(db, adminUser, administrator);
        }

        await EnsureTeamRoleAsync(db, admins, administrator);
        await EnsureTeamRoleAsync(db, intakeTeam, intakeReviewer);
        await EnsureTeamRoleAsync(db, cddTeam, intakeReviewer);
        await EnsureTeamRoleAsync(db, partnerApprovers, intakeReviewer);
        await EnsureTeamRoleAsync(db, partnerApprovers, entityManager);

        await db.SaveChangesAsync();
    }

    private static async Task EnsureStarterConflictDataAsync(MatterForgeDbContext db)
    {
        var conflictSearchService = new ConflictSearchService(db);
        await conflictSearchService.SyncExistingClientMatterPartiesAsync();
        await EnsureDemoConflictPartiesAsync(db, conflictSearchService);
    }

    private static async Task EnsureStarterSystemSettingsAsync(MatterForgeDbContext db)
    {
        var settings = new[]
        {
            new SettingSeed("General.SupportEmail", "General", "Support email", "Primary support address shown to users and used in outbound support-related messages.", "support@cmiforge.com", SystemSettingValueTypes.Email),
            new SettingSeed("General.DefaultTimeZone", "General", "Default timezone", "Fallback timezone used when the browser has not provided a local timezone cookie.", "Central Standard Time", SystemSettingValueTypes.Text),
            new SettingSeed("Email.NotificationsEnabled", "Email", "Enable email notifications", "Turns outbound workflow and system email notifications on or off once notification sending is wired.", "false", SystemSettingValueTypes.Boolean),
            new SettingSeed("Email.SmtpHost", "Email", "SMTP host", "Hostname for the SMTP server used for outbound notifications.", string.Empty, SystemSettingValueTypes.Text),
            new SettingSeed("Email.SmtpPort", "Email", "SMTP port", "Port for the SMTP server. Common values are 25, 465, and 587.", "587", SystemSettingValueTypes.Integer),
            new SettingSeed("Email.SmtpUseSsl", "Email", "Use SSL/TLS", "Whether SMTP should use SSL/TLS for outbound connections.", "true", SystemSettingValueTypes.Boolean),
            new SettingSeed("Email.SmtpUsername", "Email", "SMTP username", "Username for SMTP authentication, when required.", string.Empty, SystemSettingValueTypes.Text),
            new SettingSeed("Email.SmtpPasswordSecretName", "Email", "SMTP password secret", "Key Vault secret name or secret identifier for the SMTP password. The secret value itself should live in Key Vault, not in CMIForge settings.", string.Empty, SystemSettingValueTypes.SecretReference, IsSecret: true),
            new SettingSeed("Email.FromEmail", "Email", "From email", "Email address used as the sender for outbound CMIForge notifications.", "support@cmiforge.com", SystemSettingValueTypes.Email),
            new SettingSeed("Email.FromName", "Email", "From name", "Display name used as the sender for outbound CMIForge notifications.", "CMIForge", SystemSettingValueTypes.Text)
        };

        foreach (var seed in settings)
        {
            var setting = await db.SystemSettings.FirstOrDefaultAsync(x => x.Key == seed.Key);
            if (setting is null)
            {
                setting = new SystemSetting
                {
                    Key = seed.Key,
                    Value = seed.Value
                };
                db.SystemSettings.Add(setting);
            }

            setting.Category = seed.Category;
            setting.DisplayName = seed.DisplayName;
            setting.Description = seed.Description;
            setting.ValueType = seed.ValueType;
            setting.IsSecret = seed.IsSecret;
            setting.IsEditable = true;
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureStarterTimeEntriesAsync(MatterForgeDbContext db)
    {
        if (await db.TimeEntries.AnyAsync())
        {
            return;
        }

        var demoUser = await FindDemoUserAsync(db) ??
            await db.Users.OrderBy(x => x.SystemId).FirstOrDefaultAsync();
        if (demoUser is null)
        {
            return;
        }

        var matters = await db.Matters
            .Include(x => x.Client)
            .OrderBy(x => x.MatterNumber)
            .Take(3)
            .ToListAsync();
        if (matters.Count == 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var seeds = new (Matter Matter, DateOnly WorkDate, int Minutes, bool Billable, string Status, string Narrative)[]
        {
            (matters[0], today.AddDays(-7), 72, true, TimeEntryStatuses.Approved, "Reviewed intake materials and mapped follow-up items."),
            (matters[0], today.AddDays(-5), 45, true, TimeEntryStatuses.Submitted, "Prepared conflicts notes and matter-opening checklist."),
            (matters[Math.Min(1, matters.Count - 1)], today.AddDays(-4), 90, true, TimeEntryStatuses.Draft, "Drafted approval summary and client onboarding notes."),
            (matters[Math.Min(1, matters.Count - 1)], today.AddDays(-2), 30, false, TimeEntryStatuses.NoCharge, "Internal coordination on workflow routing."),
            (matters[Math.Min(2, matters.Count - 1)], today.AddDays(-1), 120, true, TimeEntryStatuses.Approved, "Reviewed party relationships and prepared risk summary.")
        };

        var nextNumber = (await db.TimeEntries.MaxAsync(x => (int?)x.TimeEntryNumber) ?? 0) + 1;
        foreach (var seed in seeds)
        {
            if (seed.Matter.Client is null)
            {
                continue;
            }

            db.TimeEntries.Add(new TimeEntry
            {
                TimeEntryNumber = nextNumber++,
                UserId = demoUser.Id,
                ClientId = seed.Matter.Client.Id,
                MatterId = seed.Matter.Id,
                WorkDate = seed.WorkDate,
                Minutes = seed.Minutes,
                IsBillable = seed.Billable,
                Status = seed.Status,
                Narrative = seed.Narrative
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureStarterMarketingDemoDataAsync(MatterForgeDbContext db)
    {
        var form = await db.FormDefinitions
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Key == "new-matter-intake");
        var version = form?.Versions
            .Where(x => x.IsPublished)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefault();
        if (form is null || version is null)
        {
            return;
        }

        var demoUser = await FindDemoUserAsync(db);
        var now = DateTimeOffset.UtcNow;
        var seeds = new[]
        {
            new DemoSubmissionSeed(
                "Northstar Renewable Logistics",
                "Wind Farm Supply Agreement Review",
                "Energy",
                "$48,000",
                "Review a supplier master agreement for a wind-farm equipment rollout, including indemnity, change-order, and warranty terms.",
                "Counterparty diligence packet",
                "https://example.com/cmiforge-demo/northstar-diligence"),
            new DemoSubmissionSeed(
                "Beacon Family Office",
                "Portfolio Company Acquisition Intake",
                "Corporate",
                "$125,000",
                "Open acquisition intake for a family-office buyer evaluating a manufacturing portfolio company with known related-party vendors.",
                "Buyer questionnaire",
                "https://example.com/cmiforge-demo/beacon-questionnaire"),
            new DemoSubmissionSeed(
                "Meridian Health Partners",
                "Employment Transition Review",
                "Employment",
                "$22,500",
                "Coordinate executive-transition review, restrictive covenant analysis, and onboarding documents for a regional healthcare group.",
                "Transition checklist",
                "https://example.com/cmiforge-demo/meridian-transition")
        };

        var workflowService = new WorkflowService(db);
        foreach (var seed in seeds.Select((Value, Index) => new { Value, Index }))
        {
            var submission = await EnsureDemoSubmissionAsync(db, form, version, demoUser, seed.Value, now.AddDays(-6 + seed.Index * 2));
            var instance = await workflowService.EnsureStartedAsync(submission);
            if (instance is not null && seed.Index == 1)
            {
                var firstOpenTask = await db.SubmissionWorkflowTasks
                    .Where(x => x.FormSubmissionId == submission.Id && x.Status == WorkflowStatuses.TaskOpen)
                    .OrderBy(x => x.CreatedAt)
                    .FirstOrDefaultAsync();
                if (firstOpenTask is not null)
                {
                    await workflowService.ApplyOutcomeAsync(
                        firstOpenTask.Id,
                        "primary",
                        "Initial review complete. Route to final approval after conflicts follow-up.",
                        demoUser?.Id);
                }
            }
        }

        await EnsureStarterConflictSearchesAsync(db, demoUser);
    }

    private static async Task<FormSubmission> EnsureDemoSubmissionAsync(
        MatterForgeDbContext db,
        FormDefinition form,
        FormVersion version,
        MatterForgeUser? demoUser,
        DemoSubmissionSeed seed,
        DateTimeOffset submittedAt)
    {
        var existing = await db.FormSubmissions
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.DataJson.Contains(seed.MatterName));
        if (existing is not null)
        {
            return existing;
        }

        var nextNumber = (await db.FormSubmissions.MaxAsync(x => (int?)x.SubmissionNumber) ?? 0) + 1;
        var data = new Dictionary<string, string>
        {
            ["clientName"] = seed.ClientName,
            ["matterName"] = seed.MatterName,
            ["practiceArea"] = seed.PracticeArea,
            ["estimatedFees"] = seed.EstimatedFees,
            ["assignedUser"] = DemoUserDisplayName,
            ["summary"] = seed.Summary
        };

        var submission = new FormSubmission
        {
            FormDefinitionId = form.Id,
            FormVersionId = version.Id,
            SubmissionNumber = nextNumber,
            SubmitterName = "Ima User",
            SubmitterUserId = demoUser?.Id,
            Status = SubmissionStatuses.Submitted,
            DataJson = JsonSerializer.Serialize(data, FormJson.Options),
            SubmittedAt = submittedAt
        };

        submission.Attachments.Add(new SubmissionAttachment
        {
            AttachmentType = SubmissionAttachmentTypes.Link,
            DisplayName = seed.AttachmentName,
            OriginalFileName = seed.AttachmentName,
            ContentType = "text/html",
            Url = seed.AttachmentUrl,
            UploadedByUserId = demoUser?.Id,
            CreatedAt = submittedAt.AddMinutes(7)
        });

        db.FormSubmissions.Add(submission);
        await db.SaveChangesAsync();
        return submission;
    }

    private static async Task EnsureStarterConflictSearchesAsync(MatterForgeDbContext db, MatterForgeUser? demoUser)
    {
        var conflictSearchService = new ConflictSearchService(db);
        var arcadiaMatter = await db.Matters
            .FirstOrDefaultAsync(x => x.Name == "Demo Conflicts - Legacy Supply Dispute");
        var beaconSubmission = await db.FormSubmissions
            .FirstOrDefaultAsync(x => x.DataJson.Contains("Portfolio Company Acquisition Intake"));

        var searchSeeds = new[]
        {
            new DemoConflictSearchSeed(
                "Stark & Stone acquisition review",
                "Stark Stone\nArcadia Ventures IV\nMina Caldera",
                beaconSubmission?.Id,
                arcadiaMatter?.Id),
            new DemoConflictSearchSeed(
                "New Cascadia municipal bid screen",
                "City of New Cascadia\nGlobex BioSystems\nUmbrella Risk Group",
                null,
                arcadiaMatter?.Id),
            new DemoConflictSearchSeed(
                "Wayne Wainwright finance check",
                "Wayne Wainwright Capital\nAcme Anvil Works",
                null,
                arcadiaMatter?.Id)
        };

        foreach (var seed in searchSeeds)
        {
            if (await db.ConflictSearches.AnyAsync(x => x.SearchName == seed.SearchName))
            {
                continue;
            }

            var search = await conflictSearchService.CreateAndRunSearchAsync(
                seed.SearchName,
                seed.SearchTerms,
                seed.FormSubmissionId,
                seed.MatterId,
                demoUser?.Id);

            var topResult = search.Results
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.MatchedName)
                .FirstOrDefault();
            if (topResult is not null && seed.SearchName.Contains("Stark", StringComparison.OrdinalIgnoreCase))
            {
                await conflictSearchService.ApplyResultClearanceAsync(
                    topResult.Id,
                    ConflictSearchDecisions.PotentialConflict,
                    "Similar-name prior representation found. Partner review required before matter opening.",
                    demoUser?.Id);
            }
        }
    }

    private static async Task EnsureUserNamePartsAsync(MatterForgeDbContext db)
    {
        var users = await db.Users.ToListAsync();
        foreach (var user in users)
        {
            if (!string.IsNullOrWhiteSpace(user.FirstName) || !string.IsNullOrWhiteSpace(user.LastName))
            {
                user.DisplayName = UserNameParts.BuildDisplayName(user.FirstName, user.MiddleName, user.LastName);
                continue;
            }

            var parts = UserNameParts.FromDisplayName(user.DisplayName, user.Email);
            user.FirstName = parts.FirstName;
            user.MiddleName = parts.MiddleName;
            user.LastName = parts.LastName;
            user.DisplayName = parts.DisplayName;
        }
    }

    private static async Task EnsureDemoConflictPartiesAsync(MatterForgeDbContext db, ConflictSearchService conflictSearchService)
    {
        var demoMatter = await EnsureDemoConflictMatterAsync(db, conflictSearchService);

        var starkHoldings = await EnsurePartyAsync(
            db,
            "Stark & Stone Holdings LLC",
            PartyTypes.Organization,
            "Active",
            "Demo conflicts party: intentionally similar to several aliases and affiliate names.",
            "Stark Stone",
            "StarkStone Holdings",
            "S&S Holdings",
            "Stark and Stone");
        var starkLlp = await EnsurePartyAsync(
            db,
            "Stark Stone LLP",
            PartyTypes.Organization,
            "Former Client",
            "Demo conflicts party: close-name prior counsel/adverse-party test record.",
            "StarkStone LLP",
            "Stark & Stone Legal",
            "Stark Stone Legal Partners");
        var wayneWainwright = await EnsurePartyAsync(
            db,
            "Wayne & Wainwright Capital Partners",
            PartyTypes.Organization,
            "Active",
            "Demo conflicts party: multi-token name with ampersand and finance suffix noise.",
            "Wayne Wainwright CP",
            "Wainwright Capital",
            "WW Capital Partners");
        var acme = await EnsurePartyAsync(
            db,
            "Acme Anvil Works, Inc.",
            PartyTypes.Organization,
            "Active",
            "Demo conflicts party: punctuation and corporate suffix normalization test.",
            "Acme Anvils",
            "A.A.W.",
            "Acme Works");
        var globex = await EnsurePartyAsync(
            db,
            "Globex BioSystems North America LLC",
            PartyTypes.Organization,
            "Active",
            "Demo conflicts party: regional subsidiary with fuzzy spacing variants.",
            "Globex Bio Systems",
            "Globex NA",
            "Globex North America");
        var umbrella = await EnsurePartyAsync(
            db,
            "Umbrella Risk Group plc",
            PartyTypes.Organization,
            "Prospective",
            "Demo conflicts party: risk-heavy name for search testing.",
            "Umbrella R.G.",
            "Umbrella Holdings",
            "Umbrella Group");
        var arcadiaVentures = await EnsurePartyAsync(
            db,
            "Arcadia Ventures IV LP",
            PartyTypes.Organization,
            "Active",
            "Demo conflicts party: fund suffix and roman numeral test.",
            "Arcadia Ventures 4",
            "Arcadia IV",
            "Arcadia Fund IV");
        var mina = await EnsurePartyAsync(
            db,
            "Mina Q. Caldera",
            PartyTypes.Individual,
            "Active",
            "Demo conflicts individual: middle initial and punctuation matching.",
            "Mina Caldera",
            "M. Caldera",
            "Mina Quinn Caldera");
        var jonas = await EnsurePartyAsync(
            db,
            "Jonas van der Meer",
            PartyTypes.Individual,
            "Active",
            "Demo conflicts individual: particles and spacing test.",
            "J. van der Meer",
            "Jonas Vandermeer",
            "JVDM");
        var city = await EnsurePartyAsync(
            db,
            "City of New Cascadia",
            PartyTypes.Government,
            "Active",
            "Demo conflicts government party.",
            "New Cascadia",
            "Cascadia City",
            "City of Cascadia");

        await db.SaveChangesAsync();

        await EnsureMatterPartyAsync(db, demoMatter, starkHoldings, PartyRoles.AdverseParty, "Seeded as an adverse-party conflict test.");
        await EnsureMatterPartyAsync(db, demoMatter, starkLlp, PartyRoles.OpposingCounsel, "Seeded as opposing counsel with a close-name alias.");
        await EnsureMatterPartyAsync(db, demoMatter, wayneWainwright, PartyRoles.RelatedParty, "Seeded as a financing-related party.");
        await EnsureMatterPartyAsync(db, demoMatter, acme, PartyRoles.Witness, "Seeded as a witness/vendor party.");
        await EnsureMatterPartyAsync(db, demoMatter, mina, PartyRoles.RelatedParty, "Seeded as a principal/contact test party.");
        await EnsureMatterPartyAsync(db, demoMatter, city, PartyRoles.AdverseParty, "Seeded as a government-adverse test party.");

        await EnsureRelationshipAsync(db, starkHoldings, starkLlp, PartyRelationshipTypes.Affiliate, "Similar-name affiliate used for relationship expansion tests.");
        await EnsureRelationshipAsync(db, starkHoldings, globex, PartyRelationshipTypes.AcquiredBy, "Demo acquisition relationship.");
        await EnsureRelationshipAsync(db, umbrella, globex, PartyRelationshipTypes.Related, "Shared risk review relationship.");
        await EnsureRelationshipAsync(db, arcadiaVentures, wayneWainwright, PartyRelationshipTypes.Parent, "Demo fund/manager relationship.");
        await EnsureRelationshipAsync(db, mina, starkHoldings, PartyRelationshipTypes.Contact, "Demo principal/contact relationship.");
        await EnsureRelationshipAsync(db, jonas, umbrella, PartyRelationshipTypes.Contact, "Demo contact relationship.");

        await EnsureStarterContactsAsync(db, demoMatter);

        await db.SaveChangesAsync();
    }

    private static async Task EnsureStarterContactsAsync(MatterForgeDbContext db, Matter demoMatter)
    {
        if (demoMatter.Client is null)
        {
            await db.Entry(demoMatter).Reference(x => x.Client).LoadAsync();
        }

        var minaContact = await EnsureContactAsync(
            db,
            "Mina",
            "Quinn",
            "Caldera",
            "Arcadia Sample Holdings",
            "General Counsel",
            "mina.caldera@arcadiasample.example",
            "555-0112",
            "555-0199",
            "Demo contact linked to the sample client and matter.");
        var devonContact = await EnsureContactAsync(
            db,
            "Devon",
            string.Empty,
            "Ledger",
            "Arcadia Sample Holdings",
            "Accounts Payable",
            "devon.ledger@arcadiasample.example",
            "555-0144",
            string.Empty,
            "Billing contact for address-book testing.");
        var reeseContact = await EnsureContactAsync(
            db,
            "Reese",
            string.Empty,
            "Kestrel",
            "Kestrel Risk Adjusting",
            "Claims Adjuster",
            "reese.kestrel@kestrelrisk.example",
            "555-0171",
            "555-0181",
            "Matter contact for role/link testing.");

        if (demoMatter.Client is not null)
        {
            await EnsureClientContactAsync(db, demoMatter.Client, minaContact, ContactRoles.GeneralCounsel, true, "Seeded primary client contact.");
            await EnsureClientContactAsync(db, demoMatter.Client, devonContact, ContactRoles.Billing, false, "Seeded billing contact.");
        }

        await EnsureMatterContactAsync(db, demoMatter, minaContact, ContactRoles.MatterContact, true, "Seeded primary matter contact.");
        await EnsureMatterContactAsync(db, demoMatter, reeseContact, ContactRoles.Adjuster, false, "Seeded adjuster contact.");
    }

    private static async Task<Contact> EnsureContactAsync(
        MatterForgeDbContext db,
        string firstName,
        string middleName,
        string lastName,
        string organization,
        string title,
        string email,
        string phone,
        string mobilePhone,
        string notes)
    {
        var contact = await db.Contacts.FirstOrDefaultAsync(x => x.Email == email)
            ?? db.Contacts.Local.FirstOrDefault(x => x.Email == email);
        if (contact is null)
        {
            var databaseMaxContactNumber = await db.Contacts.MaxAsync(x => (int?)x.ContactNumber) ?? 0;
            var localMaxContactNumber = db.Contacts.Local.Count == 0 ? 0 : db.Contacts.Local.Max(x => x.ContactNumber);
            contact = new Contact
            {
                ContactNumber = Math.Max(databaseMaxContactNumber, localMaxContactNumber) + 1,
                Email = email
            };
            db.Contacts.Add(contact);
        }

        contact.FirstName = firstName;
        contact.MiddleName = middleName;
        contact.LastName = lastName;
        contact.DisplayName = string.Join(" ", new[] { firstName, middleName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
        contact.Organization = organization;
        contact.Title = title;
        contact.Phone = phone;
        contact.MobilePhone = mobilePhone;
        contact.Notes = notes;
        contact.UpdatedAt = DateTimeOffset.UtcNow;

        return contact;
    }

    private static async Task EnsureClientContactAsync(
        MatterForgeDbContext db,
        Client client,
        Contact contact,
        string role,
        bool isPrimary,
        string notes)
    {
        var exists = db.ClientContacts.Local.Any(x => x.ClientId == client.Id && x.ContactId == contact.Id && x.Role == role) ||
            await db.ClientContacts.AnyAsync(x => x.ClientId == client.Id && x.ContactId == contact.Id && x.Role == role);
        if (!exists)
        {
            db.ClientContacts.Add(new ClientContact
            {
                ClientId = client.Id,
                ContactId = contact.Id,
                Role = role,
                IsPrimary = isPrimary,
                Notes = notes
            });
        }
    }

    private static async Task EnsureMatterContactAsync(
        MatterForgeDbContext db,
        Matter matter,
        Contact contact,
        string role,
        bool isPrimary,
        string notes)
    {
        var exists = db.MatterContacts.Local.Any(x => x.MatterId == matter.Id && x.ContactId == contact.Id && x.Role == role) ||
            await db.MatterContacts.AnyAsync(x => x.MatterId == matter.Id && x.ContactId == contact.Id && x.Role == role);
        if (!exists)
        {
            db.MatterContacts.Add(new MatterContact
            {
                MatterId = matter.Id,
                ContactId = contact.Id,
                Role = role,
                IsPrimary = isPrimary,
                Notes = notes
            });
        }
    }

    private static async Task<Matter> EnsureDemoConflictMatterAsync(MatterForgeDbContext db, ConflictSearchService conflictSearchService)
    {
        const string clientName = "Arcadia Sample Holdings";
        const string matterName = "Demo Conflicts - Legacy Supply Dispute";

        var client = await db.Clients.FirstOrDefaultAsync(x => x.Name == clientName);
        if (client is null)
        {
            client = new Client
            {
                ClientNumber = (await db.Clients.MaxAsync(x => (int?)x.ClientNumber) ?? 0) + 1,
                Name = clientName,
                Status = "Active",
                PrimaryContact = "Mina Q. Caldera",
                Email = "conflicts.demo@arcadiasample.example",
                Phone = "555-0108",
                Notes = "Seeded demo client for richer conflict-search testing."
            };
            db.Clients.Add(client);
            await db.SaveChangesAsync();
        }

        var matter = await db.Matters
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Name == matterName);
        if (matter is null)
        {
            matter = new Matter
            {
                MatterNumber = (await db.Matters.MaxAsync(x => (int?)x.MatterNumber) ?? 0) + 1,
                Name = matterName,
                ClientId = client.Id,
                PracticeArea = "Litigation",
                Status = "Open",
                OpenedDate = DateOnly.FromDateTime(DateTime.Today),
                Notes = "Seeded demo matter for conflicts search testing."
            };
            db.Matters.Add(matter);
            await db.SaveChangesAsync();
        }

        await conflictSearchService.EnsureMatterClientPartyAsync(client, matter);
        await db.SaveChangesAsync();
        return matter;
    }

    private static async Task<Party> EnsurePartyAsync(
        MatterForgeDbContext db,
        string name,
        string partyType,
        string status,
        string notes,
        params string[] aliases)
    {
        var normalizedName = ConflictSearchService.NormalizeName(name);
        var party = await db.Parties
            .Include(x => x.Aliases)
            .FirstOrDefaultAsync(x => x.NormalizedName == normalizedName)
            ?? db.Parties.Local.FirstOrDefault(x => x.NormalizedName == normalizedName);

        if (party is null)
        {
            var databaseMaxPartyNumber = await db.Parties.MaxAsync(x => (int?)x.PartyNumber) ?? 0;
            var localMaxPartyNumber = db.Parties.Local.Count == 0 ? 0 : db.Parties.Local.Max(x => x.PartyNumber);
            party = new Party
            {
                PartyNumber = Math.Max(databaseMaxPartyNumber, localMaxPartyNumber) + 1,
                Name = name,
                NormalizedName = normalizedName
            };
            db.Parties.Add(party);
        }

        party.PartyType = partyType;
        party.Status = status;
        party.Notes = notes;

        foreach (var alias in aliases)
        {
            await EnsurePartyAliasAsync(db, party, alias);
        }

        return party;
    }

    private static async Task EnsurePartyAliasAsync(MatterForgeDbContext db, Party party, string alias)
    {
        var normalizedAlias = ConflictSearchService.NormalizeName(alias);
        var exists = party.Aliases.Any(x => x.NormalizedAlias == normalizedAlias) ||
            db.PartyAliases.Local.Any(x => x.PartyId == party.Id && x.NormalizedAlias == normalizedAlias) ||
            await db.PartyAliases.AnyAsync(x => x.PartyId == party.Id && x.NormalizedAlias == normalizedAlias);

        if (!exists)
        {
            party.Aliases.Add(new PartyAlias
            {
                PartyId = party.Id,
                Alias = alias,
                NormalizedAlias = normalizedAlias,
                Notes = "Seeded conflict-search test alias."
            });
        }
    }

    private static async Task EnsureMatterPartyAsync(
        MatterForgeDbContext db,
        Matter matter,
        Party party,
        string role,
        string notes)
    {
        var exists = db.MatterParties.Local.Any(x => x.MatterId == matter.Id && x.PartyId == party.Id && x.Role == role) ||
            await db.MatterParties.AnyAsync(x => x.MatterId == matter.Id && x.PartyId == party.Id && x.Role == role);

        if (!exists)
        {
            db.MatterParties.Add(new MatterParty
            {
                MatterId = matter.Id,
                PartyId = party.Id,
                Role = role,
                Notes = notes
            });
        }
    }

    private static async Task EnsureRelationshipAsync(
        MatterForgeDbContext db,
        Party fromParty,
        Party toParty,
        string relationshipType,
        string notes)
    {
        var exists = db.PartyRelationships.Local.Any(x =>
                x.FromPartyId == fromParty.Id &&
                x.ToPartyId == toParty.Id &&
                x.RelationshipType == relationshipType) ||
            await db.PartyRelationships.AnyAsync(x =>
                x.FromPartyId == fromParty.Id &&
                x.ToPartyId == toParty.Id &&
                x.RelationshipType == relationshipType);

        if (!exists)
        {
            db.PartyRelationships.Add(new PartyRelationship
            {
                FromPartyId = fromParty.Id,
                ToPartyId = toParty.Id,
                RelationshipType = relationshipType,
                Notes = notes
            });
        }
    }

    private static async Task EnsureStarterFormAsync(MatterForgeDbContext db, bool seedSampleData)
    {
        var existingForm = await db.FormDefinitions
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Key == "new-matter-intake");

        if (existingForm is not null)
        {
            await EnsureClientLookupFieldAsync(db, existingForm);
            if (seedSampleData)
            {
                await EnsureAssignedUserFieldAsync(db, existingForm);
            }

            return;
        }

        var form = new FormDefinition
        {
            Name = "New Matter Intake",
            Key = "new-matter-intake",
            Description = "Starter intake form for collecting the first wave of client and matter details."
        };

        var fields = new List<FormField>
        {
            new() { Key = "clientName", Label = "Client name", Type = FieldType.Client, Required = true },
            new() { Key = "matterName", Label = "Matter name", Type = FieldType.Text, Required = true },
            new() { Key = "practiceArea", Label = "Practice area", Type = FieldType.Select, Required = true, Options = ["Corporate", "Litigation", "Real Estate", "Employment"] },
            new() { Key = "estimatedFees", Label = "Estimated fees", Type = FieldType.Currency },
            new() { Key = "summary", Label = "Matter summary", Type = FieldType.TextArea, Required = true }
        };

        if (seedSampleData)
        {
            fields.Insert(fields.Count - 1, new FormField
            {
                Key = "assignedUser",
                Label = "Assigned user",
                Type = FieldType.Select,
                Options = ["Ima User"]
            });
        }

        var schema = new FormSchema
        {
            Title = "New Matter Intake",
            Fields = fields
        };

        form.Versions.Add(new FormVersion
        {
            VersionNumber = 1,
            SchemaJson = JsonSerializer.Serialize(schema, FormJson.Options),
            PublishedAt = DateTimeOffset.UtcNow,
            IsPublished = true
        });

        db.FormDefinitions.Add(form);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureClientLookupFieldAsync(MatterForgeDbContext db, FormDefinition form)
    {
        var latestVersion = form.Versions
            .Where(x => x.IsPublished)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefault();

        if (latestVersion is null)
        {
            return;
        }

        var schema = FormJson.DeserializeSchema(latestVersion.SchemaJson);
        var clientField = schema.Fields.FirstOrDefault(x => x.Key == "clientName");
        if (clientField is null || clientField.Type == FieldType.Client)
        {
            return;
        }

        clientField.Type = FieldType.Client;
        latestVersion.SchemaJson = JsonSerializer.Serialize(schema, FormJson.Options);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureAssignedUserFieldAsync(MatterForgeDbContext db, FormDefinition form)
    {
        var latestVersion = form.Versions
            .Where(x => x.IsPublished)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefault();

        if (latestVersion is null)
        {
            return;
        }

        var schema = FormJson.DeserializeSchema(latestVersion.SchemaJson);
        var existingAssignedUserField = schema.Fields.FirstOrDefault(x => x.Key == "assignedUser");
        if (existingAssignedUserField is not null)
        {
            if (existingAssignedUserField.Options.SequenceEqual(["Gabe Williams"]))
            {
                existingAssignedUserField.Options = ["Ima User"];
                latestVersion.SchemaJson = JsonSerializer.Serialize(schema, FormJson.Options);
                await db.SaveChangesAsync();
            }

            return;
        }

        var summaryIndex = schema.Fields.FindIndex(x => x.Key == "summary");
        var assignedUserField = new FormField
        {
            Key = "assignedUser",
            Label = "Assigned user",
            Type = FieldType.Select,
            Options = ["Ima User"]
        };

        if (summaryIndex >= 0)
        {
            schema.Fields.Insert(summaryIndex, assignedUserField);
        }
        else
        {
            schema.Fields.Add(assignedUserField);
        }

        latestVersion.SchemaJson = JsonSerializer.Serialize(schema, FormJson.Options);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureStarterWorkflowAsync(MatterForgeDbContext db)
    {
        var form = await db.FormDefinitions
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Key == "new-matter-intake");
        if (form is null)
        {
            return;
        }

        var demoUser = await FindDemoUserAsync(db);
        var intakeTeam = await db.Teams.FirstOrDefaultAsync(x => x.Key == "intake-team");
        var workflow = await db.WorkflowDefinitions
            .Include(x => x.Steps)
            .FirstOrDefaultAsync(x => x.Key == "standard-intake-review");

        if (workflow is null)
        {
            workflow = new WorkflowDefinition
            {
                Name = "Standard Intake Review",
                Key = "standard-intake-review",
                Description = "Starter workflow for reviewing and approving new matter intake submissions.",
                FormDefinitionId = form.Id
            };

            db.WorkflowDefinitions.Add(workflow);
        }

        workflow.Name = "Standard Intake Review";
        workflow.Description = "Starter workflow for reviewing and approving new matter intake submissions.";
        workflow.FormDefinitionId = form.Id;
        workflow.IsActive = true;

        EnsureWorkflowStep(
            workflow,
            1,
            "Intake Review",
            "Review submitted intake details for completeness before approval.",
            null,
            intakeTeam?.Id,
            "Mark reviewed",
            SubmissionStatuses.InReview);

        EnsureWorkflowStep(
            workflow,
            2,
            "Final Approval",
            "Approve the intake so it can be converted into client and matter records.",
            demoUser?.Id,
            null,
            "Approve",
            SubmissionStatuses.Approved);

        await db.SaveChangesAsync();

        foreach (var version in form.Versions.Where(x => x.WorkflowDefinitionId is null))
        {
            version.WorkflowDefinitionId = workflow.Id;
        }

        await EnsureExistingWorkflowTaskAssignmentsAsync(db, workflow);

        await db.SaveChangesAsync();
    }

    private static async Task<MatterForgeUser?> FindDemoUserAsync(MatterForgeDbContext db)
    {
        return await db.Users.FirstOrDefaultAsync(x =>
            x.DisplayName == DemoUserDisplayName ||
            x.Email == DemoUserEmail ||
            x.Email == PreviousDemoUserEmail);
    }

    private static void EnsureWorkflowStep(
        WorkflowDefinition workflow,
        int stepNumber,
        string name,
        string instructions,
        Guid? assignedUserId,
        Guid? assignedTeamId,
        string approvalLabel,
        string completionSubmissionStatus)
    {
        var step = workflow.Steps.FirstOrDefault(x => x.StepNumber == stepNumber);
        if (step is null)
        {
            step = new WorkflowStep
            {
                StepNumber = stepNumber
            };

            workflow.Steps.Add(step);
        }

        step.Name = name;
        step.Instructions = instructions;
        step.AssignedUserId = assignedUserId;
        step.AssignedTeamId = assignedTeamId;
        step.ApprovalLabel = approvalLabel;
        step.CompletionSubmissionStatus = completionSubmissionStatus;
        if (string.IsNullOrWhiteSpace(step.OutcomesJson) || step.OutcomesJson == "[]")
        {
            step.OutcomesJson = WorkflowOutcomeParser.Serialize(WorkflowOutcomeParser.FromDesignerText(
                null,
                approvalLabel,
                completionSubmissionStatus));
        }

        if (string.IsNullOrWhiteSpace(step.ConditionOperator))
        {
            step.ConditionOperator = WorkflowStepConditionOperators.Always;
        }
    }

    private static async Task EnsureExistingWorkflowTaskAssignmentsAsync(MatterForgeDbContext db, WorkflowDefinition workflow)
    {
        foreach (var step in workflow.Steps)
        {
            var openTasks = await db.SubmissionWorkflowTasks
                .Where(x => x.WorkflowStepId == step.Id && x.Status == WorkflowStatuses.TaskOpen)
                .ToListAsync();

            foreach (var task in openTasks)
            {
                task.AssignedUserId = step.AssignedUserId;
                task.AssignedTeamId = step.AssignedTeamId;
            }
        }
    }

    private static async Task<SecurityRole> EnsureRoleAsync(
        MatterForgeDbContext db,
        string name,
        string key,
        string description,
        bool isSystem)
    {
        var role = await db.SecurityRoles.FirstOrDefaultAsync(x => x.Key == key);
        if (role is null)
        {
            role = new SecurityRole { Key = key };
            db.SecurityRoles.Add(role);
        }

        role.Name = name;
        role.Description = description;
        role.IsSystem = isSystem;
        role.IsActive = true;

        return role;
    }

    private static async Task<Team> EnsureTeamAsync(
        MatterForgeDbContext db,
        string name,
        string key,
        string description)
    {
        var team = await db.Teams.FirstOrDefaultAsync(x => x.Key == key);
        if (team is null)
        {
            team = new Team { Key = key };
            db.Teams.Add(team);
        }

        team.Name = name;
        team.Description = description;
        team.IsActive = true;

        return team;
    }

    private static async Task EnsureRolePermissionsAsync(MatterForgeDbContext db, SecurityRole role, params string[] permissionKeys)
    {
        await db.SaveChangesAsync();

        var permissions = await db.Permissions
            .Where(x => permissionKeys.Contains(x.Key))
            .ToListAsync();

        foreach (var permission in permissions)
        {
            var exists = await db.RolePermissions.AnyAsync(x => x.SecurityRoleId == role.Id && x.PermissionId == permission.Id);
            if (!exists)
            {
                db.RolePermissions.Add(new RolePermission
                {
                    SecurityRoleId = role.Id,
                    PermissionId = permission.Id
                });
            }
        }
    }

    private static async Task EnsureUserRoleAsync(MatterForgeDbContext db, MatterForgeUser user, SecurityRole role)
    {
        var exists = await db.UserRoles.AnyAsync(x => x.UserId == user.Id && x.SecurityRoleId == role.Id);
        if (!exists)
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                SecurityRoleId = role.Id
            });
        }
    }

    private static async Task EnsureTeamRoleAsync(MatterForgeDbContext db, Team team, SecurityRole role)
    {
        var exists = await db.TeamRoles.AnyAsync(x => x.TeamId == team.Id && x.SecurityRoleId == role.Id);
        if (!exists)
        {
            db.TeamRoles.Add(new TeamRole
            {
                TeamId = team.Id,
                SecurityRoleId = role.Id
            });
        }
    }

    private static async Task EnsureTeamMemberAsync(MatterForgeDbContext db, Team team, MatterForgeUser user)
    {
        var exists = await db.TeamMembers.AnyAsync(x => x.TeamId == team.Id && x.UserId == user.Id);
        if (!exists)
        {
            db.TeamMembers.Add(new TeamMember
            {
                TeamId = team.Id,
                UserId = user.Id
            });
        }
    }

    private sealed record SettingSeed(
        string Key,
        string Category,
        string DisplayName,
        string Description,
        string Value,
        string ValueType,
        bool IsSecret = false);

    private sealed record DemoSubmissionSeed(
        string ClientName,
        string MatterName,
        string PracticeArea,
        string EstimatedFees,
        string Summary,
        string AttachmentName,
        string AttachmentUrl);

    private sealed record DemoConflictSearchSeed(
        string SearchName,
        string SearchTerms,
        Guid? FormSubmissionId,
        Guid? MatterId);
}
