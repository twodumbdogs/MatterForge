using CMIForge.Models;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Data;

public class CMIForgeDbContext(DbContextOptions<CMIForgeDbContext> options) : DbContext(options)
{
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();

    public DbSet<FormVersion> FormVersions => Set<FormVersion>();

    public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Contact> Contacts => Set<Contact>();

    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();

    public DbSet<MatterContact> MatterContacts => Set<MatterContact>();

    public DbSet<Matter> Matters => Set<Matter>();

    public DbSet<CMIForgeUser> Users => Set<CMIForgeUser>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    public DbSet<SecurityRole> SecurityRoles => Set<SecurityRole>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<TeamRole> TeamRoles => Set<TeamRole>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();

    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();

    public DbSet<WorkflowNotificationTemplate> WorkflowNotificationTemplates => Set<WorkflowNotificationTemplate>();

    public DbSet<SubmissionWorkflowInstance> SubmissionWorkflowInstances => Set<SubmissionWorkflowInstance>();

    public DbSet<SubmissionWorkflowTask> SubmissionWorkflowTasks => Set<SubmissionWorkflowTask>();

    public DbSet<SubmissionWorkflowEvent> SubmissionWorkflowEvents => Set<SubmissionWorkflowEvent>();

    public DbSet<Party> Parties => Set<Party>();

    public DbSet<PartyAlias> PartyAliases => Set<PartyAlias>();

    public DbSet<MatterParty> MatterParties => Set<MatterParty>();

    public DbSet<PartyRelationship> PartyRelationships => Set<PartyRelationship>();

    public DbSet<ConflictSearch> ConflictSearches => Set<ConflictSearch>();

    public DbSet<ConflictSearchResult> ConflictSearchResults => Set<ConflictSearchResult>();

    public DbSet<ConflictSearchArchive> ConflictSearchArchives => Set<ConflictSearchArchive>();

    public DbSet<ConflictSearchHitArchive> ConflictSearchHitArchives => Set<ConflictSearchHitArchive>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<ImportBatchRow> ImportBatchRows => Set<ImportBatchRow>();

    public DbSet<SubmissionAttachment> SubmissionAttachments => Set<SubmissionAttachment>();

    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    public DbSet<TimeCodeSet> TimeCodeSets => Set<TimeCodeSet>();

    public DbSet<TimePhase> TimePhases => Set<TimePhase>();

    public DbSet<TimeTask> TimeTasks => Set<TimeTask>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();

    public DbSet<EntityChangeRequest> EntityChangeRequests => Set<EntityChangeRequest>();

    public DbSet<EnhancementRequest> EnhancementRequests => Set<EnhancementRequest>();

    public DbSet<EntityNote> EntityNotes => Set<EntityNote>();

    public DbSet<TenantProvisioningRequest> TenantProvisioningRequests => Set<TenantProvisioningRequest>();

    public DbSet<LegalAgreementAcceptance> LegalAgreementAcceptances => Set<LegalAgreementAcceptance>();

    public DbSet<DemoResetRun> DemoResetRuns => Set<DemoResetRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FormDefinition>(entity =>
        {
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Key).HasMaxLength(80);
            entity.Property(x => x.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<FormVersion>(entity =>
        {
            entity.Property(x => x.SchemaJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => new { x.FormDefinitionId, x.VersionNumber }).IsUnique();
            entity
                .HasOne(x => x.FormDefinition)
                .WithMany(x => x.Versions)
                .HasForeignKey(x => x.FormDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.WorkflowDefinition)
                .WithMany()
                .HasForeignKey(x => x.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<FormSubmission>(entity =>
        {
            entity.Property(x => x.SubmitterName).HasMaxLength(160);
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.Property(x => x.DataJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => x.SubmissionNumber).IsUnique();
            entity.HasIndex(x => new { x.Status, x.SubmissionNumber });
            entity.HasIndex(x => new { x.SubmitterUserId, x.SubmissionNumber });
            entity.HasIndex(x => x.SubmittedAt);
            entity.HasIndex(x => new { x.ClientId, x.SubmissionNumber });
            entity.HasIndex(x => new { x.MatterId, x.SubmissionNumber });
            entity.HasIndex(x => new { x.LeadPartnerId, x.SubmissionNumber });
            entity
                .HasOne(x => x.SubmitterUser)
                .WithMany()
                .HasForeignKey(x => x.SubmitterUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.LeadPartner)
                .WithMany()
                .HasForeignKey(x => x.LeadPartnerId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.FormDefinition)
                .WithMany()
                .HasForeignKey(x => x.FormDefinitionId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.FormVersion)
                .WithMany(x => x.Submissions)
                .HasForeignKey(x => x.FormVersionId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.Matter)
                .WithMany()
                .HasForeignKey(x => x.MatterId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SubmissionAttachment>(entity =>
        {
            entity.Property(x => x.AttachmentType).HasMaxLength(40);
            entity.Property(x => x.DisplayName).HasMaxLength(240);
            entity.Property(x => x.OriginalFileName).HasMaxLength(260);
            entity.Property(x => x.ContentType).HasMaxLength(160);
            entity.Property(x => x.FileExtension).HasMaxLength(20);
            entity.Property(x => x.BlobContainer).HasMaxLength(120);
            entity.Property(x => x.BlobName).HasMaxLength(700);
            entity.Property(x => x.Url).HasMaxLength(2000);
            entity.HasIndex(x => new { x.FormSubmissionId, x.CreatedAt });
            entity
                .HasOne(x => x.FormSubmission)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.FormSubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.UploadedByUser)
                .WithMany()
                .HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TimeEntry>(entity =>
        {
            entity.Property(x => x.ClientNarrative).HasMaxLength(2000);
            entity.Property(x => x.InternalNotes).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.HasIndex(x => x.TimeEntryNumber).IsUnique();
            entity.HasIndex(x => new { x.WorkDate, x.UserId });
            entity.HasIndex(x => new { x.MatterId, x.WorkDate });
            entity.HasIndex(x => new { x.Status, x.WorkDate });
            entity.HasIndex(x => new { x.ExportedAt, x.WorkDate });
            entity
                .HasOne(x => x.User)
                .WithMany(x => x.TimeEntries)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.Client)
                .WithMany(x => x.TimeEntries)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.Matter)
                .WithMany(x => x.TimeEntries)
                .HasForeignKey(x => x.MatterId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.TimePhase)
                .WithMany(x => x.TimeEntries)
                .HasForeignKey(x => x.TimePhaseId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.TimeTask)
                .WithMany(x => x.TimeEntries)
                .HasForeignKey(x => x.TimeTaskId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.ApprovedByUser)
                .WithMany()
                .HasForeignKey(x => x.ApprovedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.ExportedByUser)
                .WithMany()
                .HasForeignKey(x => x.ExportedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TimeCodeSet>(entity =>
        {
            entity.Property(x => x.Key).HasMaxLength(80);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<TimePhase>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(40);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.HasIndex(x => new { x.TimeCodeSetId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TimeCodeSetId, x.SortOrder });
            entity
                .HasOne(x => x.TimeCodeSet)
                .WithMany(x => x.Phases)
                .HasForeignKey(x => x.TimeCodeSetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TimeTask>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(40);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.HasIndex(x => new { x.TimeCodeSetId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TimeCodeSetId, x.SortOrder });
            entity
                .HasOne(x => x.TimeCodeSet)
                .WithMany(x => x.Tasks)
                .HasForeignKey(x => x.TimeCodeSetId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.TimePhase)
                .WithMany(x => x.Tasks)
                .HasForeignKey(x => x.TimePhaseId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(x => x.ActorDisplayName).HasMaxLength(160);
            entity.Property(x => x.ActorEmail).HasMaxLength(254);
            entity.Property(x => x.Action).HasMaxLength(120);
            entity.Property(x => x.EntityType).HasMaxLength(80);
            entity.Property(x => x.EntityId).HasMaxLength(80);
            entity.Property(x => x.EntityNumber).HasMaxLength(40);
            entity.Property(x => x.Summary).HasMaxLength(500);
            entity.Property(x => x.DetailsJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.IpAddress).HasMaxLength(80);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.EntityType, x.EntityId });
            entity.HasIndex(x => new { x.ActorUserId, x.CreatedAt });
            entity
                .HasOne(x => x.ActorUser)
                .WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.Property(x => x.Key).HasMaxLength(120);
            entity.Property(x => x.Category).HasMaxLength(80);
            entity.Property(x => x.DisplayName).HasMaxLength(160);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Value).HasMaxLength(2000);
            entity.Property(x => x.ValueType).HasMaxLength(40);
            entity.HasIndex(x => x.Key).IsUnique();
            entity.HasIndex(x => new { x.Category, x.DisplayName });
            entity
                .HasOne(x => x.UpdatedByUser)
                .WithMany()
                .HasForeignKey(x => x.UpdatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EmailOutboxMessage>(entity =>
        {
            entity.Property(x => x.Status).HasMaxLength(40);
            entity.Property(x => x.Provider).HasMaxLength(80);
            entity.Property(x => x.MailboxAddress).HasMaxLength(254);
            entity.Property(x => x.FromEmail).HasMaxLength(254);
            entity.Property(x => x.FromName).HasMaxLength(160);
            entity.Property(x => x.ReplyToEmail).HasMaxLength(254);
            entity.Property(x => x.ToRecipients).HasMaxLength(2000);
            entity.Property(x => x.Subject).HasMaxLength(300);
            entity.Property(x => x.Body).HasColumnType("nvarchar(max)");
            entity.Property(x => x.LastError).HasMaxLength(4000);
            entity.HasIndex(x => new { x.Status, x.NextAttemptAt, x.CreatedAt });
            entity.HasIndex(x => x.FormSubmissionId);
            entity
                .HasOne(x => x.FormSubmission)
                .WithMany()
                .HasForeignKey(x => x.FormSubmissionId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.SubmissionWorkflowInstance)
                .WithMany()
                .HasForeignKey(x => x.SubmissionWorkflowInstanceId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.WorkflowStep)
                .WithMany()
                .HasForeignKey(x => x.WorkflowStepId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TenantProvisioningRequest>(entity =>
        {
            entity.Property(x => x.FirmName).HasMaxLength(200);
            entity.Property(x => x.AdminFirstName).HasMaxLength(120);
            entity.Property(x => x.AdminLastName).HasMaxLength(120);
            entity.Property(x => x.AdminEmail).HasMaxLength(254);
            entity.Property(x => x.DesiredDomain).HasMaxLength(160);
            entity.Property(x => x.DesiredSubdomain).HasMaxLength(80);
            entity.Property(x => x.Plan).HasMaxLength(60);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.Property(x => x.InternalNotes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasIndex(x => x.AdminEmail);
            entity
                .HasOne(x => x.UpdatedByUser)
                .WithMany()
                .HasForeignKey(x => x.UpdatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<LegalAgreementAcceptance>(entity =>
        {
            entity.Property(x => x.CustomerName).HasMaxLength(200);
            entity.Property(x => x.AcceptedByName).HasMaxLength(240);
            entity.Property(x => x.AcceptedByEmail).HasMaxLength(254);
            entity.Property(x => x.AgreementKey).HasMaxLength(120);
            entity.Property(x => x.AgreementVersion).HasMaxLength(40);
            entity.Property(x => x.AgreementTitle).HasMaxLength(240);
            entity.Property(x => x.ProductVersion).HasMaxLength(80);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.HasIndex(x => x.TenantProvisioningRequestId).IsUnique();
            entity.HasIndex(x => new { x.AgreementKey, x.AgreementVersion });
            entity
                .HasOne(x => x.TenantProvisioningRequest)
                .WithOne(x => x.LegalAgreementAcceptance)
                .HasForeignKey<LegalAgreementAcceptance>(x => x.TenantProvisioningRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DemoResetRun>(entity =>
        {
            entity.Property(x => x.Trigger).HasMaxLength(120);
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.Property(x => x.Message).HasMaxLength(1000);
            entity.Property(x => x.Error).HasMaxLength(4000);
            entity.HasIndex(x => new { x.Status, x.StartedAt });
            entity.HasIndex(x => x.StartedAt);
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.Property(x => x.PrimaryContact).HasMaxLength(160);
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.Phone).HasMaxLength(60);
            entity.Property(x => x.AddressLine1).HasMaxLength(240);
            entity.Property(x => x.AddressLine2).HasMaxLength(240);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.State).HasMaxLength(80);
            entity.Property(x => x.PostalCode).HasMaxLength(40);
            entity.Property(x => x.Country).HasMaxLength(120);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => x.ClientNumber).IsUnique();
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => new { x.IsArchived, x.ClientNumber });
            entity.HasIndex(x => new { x.IsArchived, x.Name });
        });

        modelBuilder.Entity<Contact>(entity =>
        {
            entity.Property(x => x.FirstName).HasMaxLength(80);
            entity.Property(x => x.MiddleName).HasMaxLength(80);
            entity.Property(x => x.LastName).HasMaxLength(120);
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.Property(x => x.Organization).HasMaxLength(200);
            entity.Property(x => x.Title).HasMaxLength(120);
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.Phone).HasMaxLength(60);
            entity.Property(x => x.MobilePhone).HasMaxLength(60);
            entity.Property(x => x.AddressLine1).HasMaxLength(240);
            entity.Property(x => x.AddressLine2).HasMaxLength(240);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.State).HasMaxLength(80);
            entity.Property(x => x.PostalCode).HasMaxLength(40);
            entity.Property(x => x.Country).HasMaxLength(120);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => x.ContactNumber).IsUnique();
            entity.HasIndex(x => x.DisplayName);
            entity.HasIndex(x => x.Email);
            entity.HasIndex(x => new { x.IsArchived, x.ContactNumber });
            entity.HasIndex(x => new { x.IsArchived, x.DisplayName });
        });

        modelBuilder.Entity<ClientContact>(entity =>
        {
            entity.Property(x => x.Role).HasMaxLength(80);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.ClientId, x.ContactId, x.Role }).IsUnique();
            entity.HasIndex(x => new { x.ContactId, x.ClientId });
            entity
                .HasOne(x => x.Client)
                .WithMany(x => x.Contacts)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.Contact)
                .WithMany(x => x.ClientLinks)
                .HasForeignKey(x => x.ContactId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MatterContact>(entity =>
        {
            entity.Property(x => x.Role).HasMaxLength(80);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.MatterId, x.ContactId, x.Role }).IsUnique();
            entity.HasIndex(x => new { x.ContactId, x.MatterId });
            entity
                .HasOne(x => x.Matter)
                .WithMany(x => x.Contacts)
                .HasForeignKey(x => x.MatterId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.Contact)
                .WithMany(x => x.MatterLinks)
                .HasForeignKey(x => x.ContactId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EntityChangeRequest>(entity =>
        {
            entity.Property(x => x.EntityType).HasMaxLength(80);
            entity.Property(x => x.EntityNumber).HasMaxLength(40);
            entity.Property(x => x.EntityName).HasMaxLength(240);
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.Property(x => x.Summary).HasMaxLength(500);
            entity.Property(x => x.CurrentValuesJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ProposedValuesJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.RequestNotes).HasMaxLength(2000);
            entity.Property(x => x.ReviewNotes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.EntityType, x.EntityId, x.Status });
            entity.HasIndex(x => new { x.Status, x.RequestedAt });
            entity
                .HasOne(x => x.RequestedByUser)
                .WithMany()
                .HasForeignKey(x => x.RequestedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.ReviewedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EnhancementRequest>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Area).HasMaxLength(80);
            entity.Property(x => x.Priority).HasMaxLength(40);
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.BusinessValue).HasMaxLength(1000);
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.Property(x => x.InternalNotes).HasMaxLength(2000);
            entity.Property(x => x.SubmittedByDisplayName).HasMaxLength(160);
            entity.Property(x => x.SubmittedByEmail).HasMaxLength(254);
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasIndex(x => new { x.SubmittedByUserId, x.CreatedAt });
            entity
                .HasOne(x => x.SubmittedByUser)
                .WithMany()
                .HasForeignKey(x => x.SubmittedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.UpdatedByUser)
                .WithMany()
                .HasForeignKey(x => x.UpdatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EntityNote>(entity =>
        {
            entity.Property(x => x.EntityType).HasMaxLength(80);
            entity.Property(x => x.Body).HasMaxLength(2000);
            entity.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
            entity
                .HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Matter>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.PracticeArea).HasMaxLength(120);
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => x.MatterNumber).IsUnique();
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => new { x.IsArchived, x.MatterNumber });
            entity.HasIndex(x => new { x.ClientId, x.MatterNumber });
            entity.HasIndex(x => new { x.ResponsibleUserId, x.MatterNumber });
            entity.HasIndex(x => new { x.LeadPartnerId, x.MatterNumber });
            entity.HasIndex(x => x.TimeCodeSetId);
            entity
                .HasOne(x => x.Client)
                .WithMany(x => x.Matters)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.ResponsibleUser)
                .WithMany(x => x.ResponsibleMatters)
                .HasForeignKey(x => x.ResponsibleUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.LeadPartner)
                .WithMany(x => x.LeadPartnerMatters)
                .HasForeignKey(x => x.LeadPartnerId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.TimeCodeSet)
                .WithMany(x => x.Matters)
                .HasForeignKey(x => x.TimeCodeSetId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Party>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(240);
            entity.Property(x => x.NormalizedName).HasMaxLength(240);
            entity.Property(x => x.PartyType).HasMaxLength(60);
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => x.PartyNumber).IsUnique();
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.NormalizedName);
            entity.HasIndex(x => new { x.IsArchived, x.PartyNumber });
        });

        modelBuilder.Entity<PartyAlias>(entity =>
        {
            entity.Property(x => x.Alias).HasMaxLength(240);
            entity.Property(x => x.NormalizedAlias).HasMaxLength(240);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.PartyId, x.NormalizedAlias }).IsUnique();
            entity.HasIndex(x => x.NormalizedAlias);
            entity
                .HasOne(x => x.Party)
                .WithMany(x => x.Aliases)
                .HasForeignKey(x => x.PartyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MatterParty>(entity =>
        {
            entity.Property(x => x.Role).HasMaxLength(80);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.MatterId, x.PartyId, x.Role }).IsUnique();
            entity
                .HasOne(x => x.Matter)
                .WithMany(x => x.Parties)
                .HasForeignKey(x => x.MatterId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.Party)
                .WithMany(x => x.MatterParties)
                .HasForeignKey(x => x.PartyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PartyRelationship>(entity =>
        {
            entity.Property(x => x.RelationshipType).HasMaxLength(80);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.FromPartyId, x.ToPartyId, x.RelationshipType }).IsUnique();
            entity
                .HasOne(x => x.FromParty)
                .WithMany(x => x.OutboundRelationships)
                .HasForeignKey(x => x.FromPartyId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.ToParty)
                .WithMany(x => x.InboundRelationships)
                .HasForeignKey(x => x.ToPartyId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ConflictSearch>(entity =>
        {
            entity.ToTable("Conflicts");
            entity.Property(x => x.SearchName).HasMaxLength(240);
            entity.Property(x => x.SearchTerms).HasMaxLength(2000);
            entity.Property(x => x.NormalizedTerms).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(80);
            entity.Property(x => x.ReviewerDecision).HasMaxLength(80);
            entity.Property(x => x.ReviewNotes).HasMaxLength(2000);
            entity.Property(x => x.AiSummary).HasMaxLength(3000);
            entity.HasIndex(x => x.SearchNumber).IsUnique();
            entity.HasIndex(x => new { x.MatterId, x.SearchNumber });
            entity.HasIndex(x => new { x.FormSubmissionId, x.SearchNumber });
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasIndex(x => new { x.ReviewerDecision, x.CreatedAt });
            entity.HasIndex(x => new { x.ArchivedAt, x.SearchNumber });
            entity
                .HasOne(x => x.FormSubmission)
                .WithMany()
                .HasForeignKey(x => x.FormSubmissionId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.Matter)
                .WithMany()
                .HasForeignKey(x => x.MatterId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.RequestedByUser)
                .WithMany()
                .HasForeignKey(x => x.RequestedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.ReviewedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ConflictSearchResult>(entity =>
        {
            entity.ToTable("ConflictsResults");
            entity.Property(x => x.SearchTerm).HasMaxLength(240);
            entity.Property(x => x.MatchedName).HasMaxLength(240);
            entity.Property(x => x.MatchedOn).HasMaxLength(240);
            entity.Property(x => x.MatchType).HasMaxLength(80);
            entity.Property(x => x.PartyRole).HasMaxLength(80);
            entity.Property(x => x.RiskLevel).HasMaxLength(40);
            entity.Property(x => x.Explanation).HasMaxLength(2000);
            entity.Property(x => x.AiAssessment).HasMaxLength(2000);
            entity.Property(x => x.ClearanceStatus).HasMaxLength(80).HasDefaultValue(ConflictSearchDecisions.Pending);
            entity.Property(x => x.ClearanceNotes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.ConflictSearchId, x.Score });
            entity.HasIndex(x => new { x.ConflictSearchId, x.ClearanceStatus });
            entity.HasIndex(x => new { x.ClearedByUserId, x.ClearedAt });
            entity
                .HasOne(x => x.ConflictSearch)
                .WithMany(x => x.Results)
                .HasForeignKey(x => x.ConflictSearchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.Party)
                .WithMany()
                .HasForeignKey(x => x.PartyId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.Matter)
                .WithMany()
                .HasForeignKey(x => x.MatterId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.ClearedByUser)
                .WithMany()
                .HasForeignKey(x => x.ClearedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ConflictSearchArchive>(entity =>
        {
            entity.ToTable("ConflictsSearchArchives");
            entity.Property(x => x.SearchName).HasMaxLength(240);
            entity.Property(x => x.SearchTerms).HasMaxLength(2000);
            entity.Property(x => x.NormalizedTerms).HasMaxLength(2000);
            entity.Property(x => x.SearchableText).HasColumnType("nvarchar(max)");
            entity.Property(x => x.NormalizedSearchableText).HasColumnType("nvarchar(max)");
            entity.Property(x => x.FormName).HasMaxLength(160);
            entity.Property(x => x.MatterName).HasMaxLength(200);
            entity.Property(x => x.ClientName).HasMaxLength(200);
            entity.Property(x => x.RequestedByDisplayName).HasMaxLength(160);
            entity.Property(x => x.ReviewedByDisplayName).HasMaxLength(160);
            entity.Property(x => x.Status).HasMaxLength(80);
            entity.Property(x => x.ReviewerDecision).HasMaxLength(80);
            entity.Property(x => x.ReviewNotes).HasMaxLength(2000);
            entity.Property(x => x.AiSummary).HasMaxLength(3000);
            entity.Property(x => x.HighestRiskLevel).HasMaxLength(40);
            entity.Property(x => x.PayloadCompression).HasMaxLength(40);
            entity.Property(x => x.PayloadBytes).HasColumnType("varbinary(max)");
            entity.HasIndex(x => x.ConflictSearchId).IsUnique();
            entity.HasIndex(x => x.SearchNumber).IsUnique();
            entity.HasIndex(x => new { x.ArchivedAt, x.SearchNumber });
            entity.HasIndex(x => new { x.MatterId, x.SearchNumber });
            entity.HasIndex(x => new { x.ClientId, x.SearchNumber });
            entity.HasIndex(x => new { x.Status, x.ArchivedAt });
            entity.HasIndex(x => new { x.ReviewerDecision, x.ArchivedAt });
            entity
                .HasOne(x => x.ConflictSearch)
                .WithOne()
                .HasForeignKey<ConflictSearchArchive>(x => x.ConflictSearchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.FormSubmission)
                .WithMany()
                .HasForeignKey(x => x.FormSubmissionId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.Matter)
                .WithMany()
                .HasForeignKey(x => x.MatterId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.RequestedByUser)
                .WithMany()
                .HasForeignKey(x => x.RequestedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.ReviewedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ConflictSearchHitArchive>(entity =>
        {
            entity.ToTable("ConflictsHitArchives");
            entity.Property(x => x.SearchTerm).HasMaxLength(240);
            entity.Property(x => x.MatchedName).HasMaxLength(240);
            entity.Property(x => x.MatchedOn).HasMaxLength(240);
            entity.Property(x => x.MatchType).HasMaxLength(80);
            entity.Property(x => x.PartyRole).HasMaxLength(80);
            entity.Property(x => x.PartyName).HasMaxLength(240);
            entity.Property(x => x.MatterName).HasMaxLength(200);
            entity.Property(x => x.ClientName).HasMaxLength(200);
            entity.Property(x => x.RiskLevel).HasMaxLength(40);
            entity.Property(x => x.Explanation).HasMaxLength(2000);
            entity.Property(x => x.AiAssessment).HasMaxLength(2000);
            entity.Property(x => x.ClearanceStatus).HasMaxLength(80);
            entity.Property(x => x.ClearanceNotes).HasMaxLength(2000);
            entity.Property(x => x.ClearedByDisplayName).HasMaxLength(160);
            entity.Property(x => x.SearchableText).HasColumnType("nvarchar(max)");
            entity.Property(x => x.NormalizedSearchableText).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => new { x.ConflictSearchArchiveId, x.Score });
            entity.HasIndex(x => new { x.ConflictSearchArchiveId, x.ClearanceStatus });
            entity.HasIndex(x => new { x.ConflictSearchId, x.Score });
            entity.HasIndex(x => new { x.MatterId, x.SearchNumber });
            entity.HasIndex(x => new { x.ClientId, x.SearchNumber });
            entity.HasIndex(x => new { x.PartyId, x.SearchNumber });
            entity
                .HasOne(x => x.ConflictSearchArchive)
                .WithMany(x => x.Hits)
                .HasForeignKey(x => x.ConflictSearchArchiveId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.Party)
                .WithMany()
                .HasForeignKey(x => x.PartyId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.Matter)
                .WithMany()
                .HasForeignKey(x => x.MatterId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.ClearedByUser)
                .WithMany()
                .HasForeignKey(x => x.ClearedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CMIForgeUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(x => x.FirstName).HasMaxLength(80);
            entity.Property(x => x.MiddleName).HasMaxLength(80);
            entity.Property(x => x.LastName).HasMaxLength(120);
            entity.Property(x => x.DisplayName).HasMaxLength(160);
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.EntraTenantId).HasMaxLength(80);
            entity.Property(x => x.EntraObjectId).HasMaxLength(80);
            entity.Property(x => x.EntraUserPrincipalName).HasMaxLength(254);
            entity.Property(x => x.Title).HasMaxLength(120);
            entity.HasIndex(x => x.LastLoginAt);
            entity.HasIndex(x => x.SystemId).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => new { x.IsArchived, x.SystemId });
            entity
                .HasIndex(x => new { x.EntraTenantId, x.EntraObjectId })
                .IsUnique()
                .HasFilter("[EntraTenantId] <> '' AND [EntraObjectId] <> ''");
            entity
                .HasIndex(x => x.EntraUserPrincipalName)
                .IsUnique()
                .HasFilter("[EntraUserPrincipalName] <> ''");
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.Property(x => x.ImportType).HasMaxLength(60);
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.Status).HasMaxLength(80);
            entity.Property(x => x.Summary).HasMaxLength(2000);
            entity.HasIndex(x => new { x.ImportType, x.CreatedAt });
            entity
                .HasOne(x => x.ImportedByUser)
                .WithMany()
                .HasForeignKey(x => x.ImportedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ImportBatchRow>(entity =>
        {
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.Property(x => x.Message).HasMaxLength(2000);
            entity.Property(x => x.SourceJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => new { x.ImportBatchId, x.RowNumber }).IsUnique();
            entity
                .HasOne(x => x.ImportBatch)
                .WithMany(x => x.Rows)
                .HasForeignKey(x => x.ImportBatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Key).HasMaxLength(80);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasIndex(x => new { x.TeamId, x.UserId }).IsUnique();
            entity
                .HasOne(x => x.Team)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.User)
                .WithMany(x => x.TeamMemberships)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SecurityRole>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Key).HasMaxLength(80);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.SecurityRoleId }).IsUnique();
            entity
                .HasOne(x => x.User)
                .WithMany(x => x.Roles)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.SecurityRole)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.SecurityRoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamRole>(entity =>
        {
            entity.HasIndex(x => new { x.TeamId, x.SecurityRoleId }).IsUnique();
            entity
                .HasOne(x => x.Team)
                .WithMany(x => x.Roles)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.SecurityRole)
                .WithMany(x => x.TeamRoles)
                .HasForeignKey(x => x.SecurityRoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.Property(x => x.Key).HasMaxLength(120);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Category).HasMaxLength(80);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasIndex(x => new { x.SecurityRoleId, x.PermissionId }).IsUnique();
            entity
                .HasOne(x => x.SecurityRole)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.SecurityRoleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.Permission)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowDefinition>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Key).HasMaxLength(80);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => x.Key).IsUnique();
            entity
                .HasOne(x => x.FormDefinition)
                .WithMany()
                .HasForeignKey(x => x.FormDefinitionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<WorkflowStep>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Instructions).HasMaxLength(1000);
            entity.Property(x => x.StepType).HasMaxLength(40);
            entity.Property(x => x.ApprovalLabel).HasMaxLength(60);
            entity.Property(x => x.CompletionSubmissionStatus).HasMaxLength(60);
            entity.Property(x => x.OutcomesJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ConditionFieldKey).HasMaxLength(80);
            entity.Property(x => x.ConditionOperator).HasMaxLength(40);
            entity.Property(x => x.ConditionValue).HasMaxLength(200);
            entity.Property(x => x.NotificationSubject).HasMaxLength(200);
            entity.Property(x => x.NotificationBody).HasColumnType("nvarchar(max)");
            entity.Property(x => x.NotificationRecipients).HasMaxLength(1000);
            entity.HasIndex(x => new { x.WorkflowDefinitionId, x.StepNumber }).IsUnique();
            entity.HasIndex(x => x.NotificationTemplateId);
            entity
                .HasOne(x => x.WorkflowDefinition)
                .WithMany(x => x.Steps)
                .HasForeignKey(x => x.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.AssignedUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.AssignedTeam)
                .WithMany()
                .HasForeignKey(x => x.AssignedTeamId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.NotificationTemplate)
                .WithMany(x => x.WorkflowSteps)
                .HasForeignKey(x => x.NotificationTemplateId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<WorkflowNotificationTemplate>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Key).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Subject).HasMaxLength(300);
            entity.Property(x => x.Body).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => x.Key).IsUnique();
            entity.HasIndex(x => new { x.IsActive, x.Name });
        });

        modelBuilder.Entity<SubmissionWorkflowInstance>(entity =>
        {
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.HasIndex(x => new { x.FormSubmissionId, x.WorkflowDefinitionId }).IsUnique();
            entity
                .HasOne(x => x.FormSubmission)
                .WithMany(x => x.WorkflowInstances)
                .HasForeignKey(x => x.FormSubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.WorkflowDefinition)
                .WithMany(x => x.Instances)
                .HasForeignKey(x => x.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SubmissionWorkflowTask>(entity =>
        {
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.Property(x => x.Outcome).HasMaxLength(60);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.FormSubmissionId, x.Status });
            entity.HasIndex(x => new { x.AssignedUserId, x.Status });
            entity.HasIndex(x => new { x.AssignedTeamId, x.Status });
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasIndex(x => new { x.AssignedUserId, x.Status, x.CreatedAt });
            entity.HasIndex(x => new { x.AssignedTeamId, x.Status, x.CreatedAt });
            entity
                .HasOne(x => x.SubmissionWorkflowInstance)
                .WithMany(x => x.Tasks)
                .HasForeignKey(x => x.SubmissionWorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.FormSubmission)
                .WithMany(x => x.WorkflowTasks)
                .HasForeignKey(x => x.FormSubmissionId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.WorkflowStep)
                .WithMany(x => x.Tasks)
                .HasForeignKey(x => x.WorkflowStepId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.AssignedUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.AssignedTeam)
                .WithMany()
                .HasForeignKey(x => x.AssignedTeamId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.CompletedByUser)
                .WithMany()
                .HasForeignKey(x => x.CompletedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SubmissionWorkflowEvent>(entity =>
        {
            entity.Property(x => x.EventType).HasMaxLength(80);
            entity.Property(x => x.Message).HasMaxLength(1000);
            entity.HasIndex(x => new { x.FormSubmissionId, x.CreatedAt });
            entity
                .HasOne(x => x.SubmissionWorkflowInstance)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.SubmissionWorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.FormSubmission)
                .WithMany()
                .HasForeignKey(x => x.FormSubmissionId)
                .OnDelete(DeleteBehavior.NoAction);
            entity
                .HasOne(x => x.ActorUser)
                .WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}

