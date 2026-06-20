using CMIForge.Data;
using CMIForge.Models;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public class WorkflowService(CMIForgeDbContext db, WorkflowNotificationService? notificationService = null)
{
    public async Task<bool> CanStartAsync(FormSubmission submission)
    {
        if (submission.Status is SubmissionStatuses.Converted or SubmissionStatuses.Cancelled)
        {
            return false;
        }

        var workflow = await GetWorkflowDefinitionAsync(submission.FormVersionId, submission.FormDefinitionId);
        if (workflow is null || workflow.Steps.Count == 0)
        {
            return false;
        }

        var existing = await db.SubmissionWorkflowInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.FormSubmissionId == submission.Id && x.WorkflowDefinitionId == workflow.Id);
        if (existing is not null && existing.Status != WorkflowStatuses.Cancelled)
        {
            return false;
        }

        return GetNextMatchingStep(workflow.Steps, submission, null) is not null;
    }

    public async Task<SubmissionWorkflowInstance?> EnsureStartedAsync(Guid formSubmissionId)
    {
        var submission = await db.FormSubmissions
            .Include(x => x.FormDefinition)
            .FirstOrDefaultAsync(x => x.Id == formSubmissionId);

        return submission is null ? null : await EnsureStartedAsync(submission);
    }

    public async Task<SubmissionWorkflowInstance?> EnsureStartedAsync(FormSubmission submission)
    {
        if (submission.Status is SubmissionStatuses.Converted or SubmissionStatuses.Cancelled)
        {
            return null;
        }

        var workflow = await GetWorkflowDefinitionAsync(submission.FormVersionId, submission.FormDefinitionId);
        if (workflow is null || workflow.Steps.Count == 0)
        {
            return null;
        }

        var existing = await db.SubmissionWorkflowInstances
            .Include(x => x.Tasks)
            .FirstOrDefaultAsync(x => x.FormSubmissionId == submission.Id && x.WorkflowDefinitionId == workflow.Id);

        var firstStep = GetNextMatchingStep(workflow.Steps, submission, null);
        if (firstStep is null)
        {
            return null;
        }

        if (existing is not null)
        {
            if (existing.Status != WorkflowStatuses.Cancelled)
            {
                return existing;
            }

            existing.Status = WorkflowStatuses.Active;
            existing.CompletedAt = null;
            existing.CurrentStepNumber = firstStep.StepNumber;
            existing.Events.Add(new SubmissionWorkflowEvent
            {
                FormSubmissionId = submission.Id,
                EventType = WorkflowStatuses.EventStarted,
                Message = $"Restarted workflow after cancellation: {workflow.Name}"
            });

            submission.Status = SubmissionStatuses.InReview;
            await AddNextWorkflowWorkAsync(existing, submission, workflow, firstStep);
            await db.SaveChangesAsync();
            return existing;
        }

        var instance = new SubmissionWorkflowInstance
        {
            FormSubmissionId = submission.Id,
            WorkflowDefinitionId = workflow.Id,
            Status = WorkflowStatuses.Active,
            CurrentStepNumber = firstStep.StepNumber
        };

        instance.Events.Add(new SubmissionWorkflowEvent
        {
            FormSubmissionId = submission.Id,
            EventType = WorkflowStatuses.EventStarted,
            Message = $"Started workflow: {workflow.Name}"
        });

        submission.Status = SubmissionStatuses.InReview;

        db.SubmissionWorkflowInstances.Add(instance);
        await AddNextWorkflowWorkAsync(instance, submission, workflow, firstStep);
        await db.SaveChangesAsync();

        return instance;
    }

    public async Task CompleteTaskAsync(Guid taskId, string notes, Guid? actorUserId = null)
    {
        await ApplyOutcomeAsync(taskId, "primary", notes, actorUserId);
    }

    public async Task ReturnTaskAsync(Guid taskId, string notes, Guid? actorUserId = null)
    {
        await ApplyOutcomeAsync(taskId, "return", notes, actorUserId);
    }

    public async Task ApplyOutcomeAsync(Guid taskId, string outcomeKey, string notes, Guid? actorUserId = null)
    {
        var task = await LoadOpenTaskAsync(taskId);
        if (task is null || task.SubmissionWorkflowInstance is null || task.WorkflowStep is null || task.FormSubmission is null)
        {
            return;
        }

        var instance = task.SubmissionWorkflowInstance;
        var currentStep = task.WorkflowStep;
        var workflow = await db.WorkflowDefinitions
            .Include(x => x.Steps)
            .FirstAsync(x => x.Id == instance.WorkflowDefinitionId);
        var outcome = WorkflowOutcomeParser.GetOutcomes(currentStep)
            .FirstOrDefault(x => x.Key.Equals(outcomeKey, StringComparison.OrdinalIgnoreCase))
            ?? WorkflowOutcomeParser.GetOutcomes(currentStep).First();

        var isReturned = outcome.Action.Equals(WorkflowOutcomeActions.Return, StringComparison.OrdinalIgnoreCase);
        task.Status = isReturned ? WorkflowStatuses.TaskReturned : WorkflowStatuses.TaskCompleted;
        task.Outcome = outcome.Label;
        task.Notes = notes?.Trim() ?? string.Empty;
        task.CompletedAt = DateTimeOffset.UtcNow;
        task.CompletedByUserId = actorUserId;

        task.FormSubmission.Status = outcome.SubmissionStatus;

        db.SubmissionWorkflowEvents.Add(new SubmissionWorkflowEvent
        {
            SubmissionWorkflowInstanceId = instance.Id,
            FormSubmissionId = task.FormSubmissionId,
            EventType = isReturned ? WorkflowStatuses.EventReturned : WorkflowStatuses.EventStepCompleted,
            Message = $"{currentStep.Name}: {outcome.Label}",
            ActorUserId = actorUserId
        });

        if (isReturned)
        {
            instance.Status = WorkflowStatuses.Returned;
            instance.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return;
        }

        var shouldComplete = outcome.Action.Equals(WorkflowOutcomeActions.Complete, StringComparison.OrdinalIgnoreCase);
        var nextStep = shouldComplete
            ? null
            : GetNextMatchingStep(workflow.Steps, task.FormSubmission, currentStep.StepNumber, outcome.NextStepNumber);

        await AddNextWorkflowWorkAsync(instance, task.FormSubmission, workflow, nextStep, actorUserId);

        await db.SaveChangesAsync();
    }

    private async Task AddNextWorkflowWorkAsync(
        SubmissionWorkflowInstance instance,
        FormSubmission submission,
        WorkflowDefinition workflow,
        WorkflowStep? nextStep,
        Guid? actorUserId = null)
    {
        var step = nextStep;
        while (step is not null)
        {
            instance.CurrentStepNumber = step.StepNumber;

            if (IsNotificationStep(step))
            {
                if (notificationService is null)
                {
                    db.SubmissionWorkflowEvents.Add(new SubmissionWorkflowEvent
                    {
                        SubmissionWorkflowInstanceId = instance.Id,
                        FormSubmissionId = submission.Id,
                        EventType = WorkflowStatuses.EventNotificationSkipped,
                        Message = $"Notification step '{step.Name}' skipped because notification service is unavailable.",
                        ActorUserId = actorUserId
                    });
                }
                else
                {
                    await notificationService.ProcessAsync(instance, submission, workflow, step, actorUserId);
                }

                step = GetNextMatchingStep(workflow.Steps, submission, step.StepNumber);
                continue;
            }

            var task = CreateTask(submission.Id, step);
            task.SubmissionWorkflowInstanceId = instance.Id;
            db.SubmissionWorkflowTasks.Add(task);
            return;
        }

        instance.Status = WorkflowStatuses.Completed;
        instance.CompletedAt = DateTimeOffset.UtcNow;
        db.SubmissionWorkflowEvents.Add(new SubmissionWorkflowEvent
        {
            SubmissionWorkflowInstanceId = instance.Id,
            FormSubmissionId = submission.Id,
            EventType = WorkflowStatuses.EventCompleted,
            Message = $"{workflow.Name} completed",
            ActorUserId = actorUserId
        });
    }

    private async Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(Guid formVersionId, Guid formDefinitionId)
    {
        var versionWorkflowId = await db.FormVersions
            .Where(x => x.Id == formVersionId)
            .Select(x => x.WorkflowDefinitionId)
            .FirstOrDefaultAsync();

        if (versionWorkflowId.HasValue)
        {
            return await db.WorkflowDefinitions
                .Include(x => x.Steps)
                .FirstOrDefaultAsync(x => x.Id == versionWorkflowId.Value && x.IsActive);
        }

        return await db.WorkflowDefinitions
            .Include(x => x.Steps)
            .Where(x => x.IsActive && (x.FormDefinitionId == formDefinitionId || x.FormDefinitionId == null))
            .OrderByDescending(x => x.FormDefinitionId == formDefinitionId)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync();
    }

    private async Task<SubmissionWorkflowTask?> LoadOpenTaskAsync(Guid taskId)
    {
        return await db.SubmissionWorkflowTasks
            .Include(x => x.FormSubmission)
            .Include(x => x.WorkflowStep)
                .ThenInclude(x => x!.AssignedTeam)
            .Include(x => x.SubmissionWorkflowInstance)
                .ThenInclude(x => x!.Events)
            .FirstOrDefaultAsync(x => x.Id == taskId && x.Status == WorkflowStatuses.TaskOpen);
    }

    private static SubmissionWorkflowTask CreateTask(Guid formSubmissionId, WorkflowStep step)
    {
        return new SubmissionWorkflowTask
        {
            FormSubmissionId = formSubmissionId,
            WorkflowStepId = step.Id,
            AssignedUserId = step.AssignedUserId,
            AssignedTeamId = step.AssignedTeamId,
            Status = WorkflowStatuses.TaskOpen
        };
    }

    private static bool IsNotificationStep(WorkflowStep step)
    {
        return step.StepType.Equals(WorkflowStepTypes.Notification, StringComparison.OrdinalIgnoreCase);
    }

    private static WorkflowStep? GetNextMatchingStep(
        IEnumerable<WorkflowStep> steps,
        FormSubmission submission,
        int? afterStepNumber,
        int? explicitNextStepNumber = null)
    {
        if (explicitNextStepNumber.HasValue)
        {
            return steps
                .Where(x => x.StepNumber >= explicitNextStepNumber.Value)
                .OrderBy(x => x.StepNumber)
                .FirstOrDefault(x => WorkflowStepConditions.Matches(x, submission));
        }

        var orderedSteps = steps.OrderBy(x => x.StepNumber);
        if (afterStepNumber.HasValue)
        {
            orderedSteps = orderedSteps.Where(x => x.StepNumber > afterStepNumber.Value).OrderBy(x => x.StepNumber);
        }

        return orderedSteps.FirstOrDefault(x => WorkflowStepConditions.Matches(x, submission));
    }
}
