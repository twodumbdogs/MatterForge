using MatterForge.Models;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Data;

public class MatterForgeDbContext(DbContextOptions<MatterForgeDbContext> options) : DbContext(options)
{
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();

    public DbSet<FormVersion> FormVersions => Set<FormVersion>();

    public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Matter> Matters => Set<Matter>();

    public DbSet<MatterForgeUser> Users => Set<MatterForgeUser>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    public DbSet<SecurityRole> SecurityRoles => Set<SecurityRole>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<TeamRole> TeamRoles => Set<TeamRole>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();

    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();

    public DbSet<SubmissionWorkflowInstance> SubmissionWorkflowInstances => Set<SubmissionWorkflowInstance>();

    public DbSet<SubmissionWorkflowTask> SubmissionWorkflowTasks => Set<SubmissionWorkflowTask>();

    public DbSet<SubmissionWorkflowEvent> SubmissionWorkflowEvents => Set<SubmissionWorkflowEvent>();

    public DbSet<Party> Parties => Set<Party>();

    public DbSet<PartyAlias> PartyAliases => Set<PartyAlias>();

    public DbSet<MatterParty> MatterParties => Set<MatterParty>();

    public DbSet<PartyRelationship> PartyRelationships => Set<PartyRelationship>();

    public DbSet<ConflictSearch> ConflictSearches => Set<ConflictSearch>();

    public DbSet<ConflictSearchResult> ConflictSearchResults => Set<ConflictSearchResult>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<ImportBatchRow> ImportBatchRows => Set<ImportBatchRow>();

    public DbSet<SubmissionAttachment> SubmissionAttachments => Set<SubmissionAttachment>();

    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

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
            entity
                .HasOne(x => x.SubmitterUser)
                .WithMany()
                .HasForeignKey(x => x.SubmitterUserId)
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
            entity.Property(x => x.Narrative).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.HasIndex(x => x.TimeEntryNumber).IsUnique();
            entity.HasIndex(x => new { x.WorkDate, x.UserId });
            entity.HasIndex(x => new { x.MatterId, x.WorkDate });
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

        modelBuilder.Entity<Client>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.Property(x => x.PrimaryContact).HasMaxLength(160);
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.Phone).HasMaxLength(60);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => x.ClientNumber).IsUnique();
        });

        modelBuilder.Entity<Matter>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.PracticeArea).HasMaxLength(120);
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => x.MatterNumber).IsUnique();
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
        });

        modelBuilder.Entity<Party>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(240);
            entity.Property(x => x.NormalizedName).HasMaxLength(240);
            entity.Property(x => x.PartyType).HasMaxLength(60);
            entity.Property(x => x.Status).HasMaxLength(60);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => x.PartyNumber).IsUnique();
            entity.HasIndex(x => x.NormalizedName);
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
            entity.HasIndex(x => new { x.ConflictSearchId, x.Score });
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
        });

        modelBuilder.Entity<MatterForgeUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(x => x.FirstName).HasMaxLength(80);
            entity.Property(x => x.MiddleName).HasMaxLength(80);
            entity.Property(x => x.LastName).HasMaxLength(120);
            entity.Property(x => x.DisplayName).HasMaxLength(160);
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.Title).HasMaxLength(120);
            entity.HasIndex(x => x.SystemId).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
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
            entity.Property(x => x.ApprovalLabel).HasMaxLength(60);
            entity.Property(x => x.CompletionSubmissionStatus).HasMaxLength(60);
            entity.Property(x => x.OutcomesJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ConditionFieldKey).HasMaxLength(80);
            entity.Property(x => x.ConditionOperator).HasMaxLength(40);
            entity.Property(x => x.ConditionValue).HasMaxLength(200);
            entity.HasIndex(x => new { x.WorkflowDefinitionId, x.StepNumber }).IsUnique();
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
