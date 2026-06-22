using CMIForge.Data;
using CMIForge.Models;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public class DemoResetService(
    IDbContextFactory<CMIForgeDbContext> dbFactory,
    DemoModeService demoModeService,
    SubmissionAttachmentService attachmentService,
    ILogger<DemoResetService> logger)
{
    private static readonly SemaphoreSlim ResetLock = new(1, 1);

    public async Task<DemoResetResult> ResetAsync(string trigger, CancellationToken cancellationToken = default)
    {
        if (!demoModeService.IsEnabled)
        {
            return new DemoResetResult(false, 0, "Demo reset is only available while demo mode is enabled.");
        }

        if (!await ResetLock.WaitAsync(0, cancellationToken))
        {
            await RecordResetRunAsync(
                trigger,
                DemoResetRunStatuses.Skipped,
                0,
                "A demo reset is already running.",
                string.Empty,
                cancellationToken);
            return new DemoResetResult(false, 0, "A demo reset is already running.");
        }

        var runId = await StartResetRunAsync(trigger, cancellationToken);

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                var deletedRows = 0;
                deletedRows += await db.InboundEmailAttachments.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await DeleteAttachmentsAsync(db, cancellationToken);
                deletedRows += await db.AuditLogs.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.EnhancementRequestVotes.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.EnhancementRequests.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.EntityNotes.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.EntityChangeRequests.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.LegalAgreementAcceptances.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.TenantProvisioningRequests.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.TimeEntries.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.ConflictSearchResults.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.ConflictSearches.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.ConflictSearchHitArchives.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.ConflictSearchArchives.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.ImportBatchRows.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.ImportBatches.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.SubmissionWorkflowEvents.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.SubmissionWorkflowTasks.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.SubmissionWorkflowInstances.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.EmailOutboxMessages.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.ExternalFormInvites.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.InboundEmailMessages.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.FormSubmissions.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.MatterParties.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.MatterContacts.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.ClientContacts.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.PartyRelationships.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.PartyAliases.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.ClientAliases.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.Parties.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.Contacts.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.Matters.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.Clients.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.TimeTasks.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.TimePhases.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.TimeCodeSets.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.WorkflowSteps.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.FormVersions.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.WorkflowDefinitions.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.WorkflowNotificationTemplates.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.FormDefinitions.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.TeamRoles.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.TeamMembers.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.UserRoles.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.RolePermissions.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.SecurityRoles.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.Permissions.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.Teams.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.SystemSettings.ExecuteDeleteAsync(cancellationToken);
                deletedRows += await db.Users.ExecuteDeleteAsync(cancellationToken);

                db.ChangeTracker.Clear();
                await SeedData.EnsureApplicationSeedDataAsync(db);

                var run = await db.DemoResetRuns.FirstAsync(x => x.Id == runId, cancellationToken);
                run.Status = DemoResetRunStatuses.Succeeded;
                run.DeletedRows = deletedRows;
                run.Message = "Demo data has been reset to the starter dataset.";
                run.CompletedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation("Demo reset completed from {Trigger}; deleted {DeletedRows} rows.", trigger, deletedRows);
                return new DemoResetResult(true, deletedRows, "Demo data has been reset to the starter dataset.");
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Demo reset failed from {Trigger}.", trigger);
            await RecordResetRunAsync(
                trigger,
                DemoResetRunStatuses.Failed,
                0,
                "Demo reset failed. Check application logs for details.",
                ex.Message,
                CancellationToken.None,
                runId);
            return new DemoResetResult(false, 0, "Demo reset failed. Check application logs for details.");
        }
        finally
        {
            ResetLock.Release();
        }
    }

    private async Task<int> DeleteAttachmentsAsync(CMIForgeDbContext db, CancellationToken cancellationToken)
    {
        var attachments = await db.SubmissionAttachments.ToListAsync(cancellationToken);
        foreach (var attachment in attachments)
        {
            try
            {
                await attachmentService.DeleteFileIfExistsAsync(attachment);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not delete demo attachment blob {BlobName}. The database reset will continue.", attachment.BlobName);
            }
        }

        return await db.SubmissionAttachments.ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<Guid> StartResetRunAsync(string trigger, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = new DemoResetRun
        {
            Trigger = trigger,
            Status = DemoResetRunStatuses.Running,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.DemoResetRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return run.Id;
    }

    private async Task RecordResetRunAsync(
        string trigger,
        string status,
        int deletedRows,
        string message,
        string error,
        CancellationToken cancellationToken,
        Guid? existingRunId = null)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            DemoResetRun? run = null;
            if (existingRunId.HasValue)
            {
                run = await db.DemoResetRuns.FirstOrDefaultAsync(x => x.Id == existingRunId.Value, cancellationToken);
            }

            if (run is null)
            {
                run = new DemoResetRun
                {
                    Id = existingRunId ?? Guid.NewGuid(),
                    Trigger = trigger,
                    StartedAt = DateTimeOffset.UtcNow
                };
                db.DemoResetRuns.Add(run);
            }

            run.Status = status;
            run.DeletedRows = deletedRows;
            run.Message = message;
            run.Error = error;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not record demo reset run status for {Trigger}.", trigger);
        }
    }
}

public sealed record DemoResetResult(bool Succeeded, int DeletedRows, string Message);
