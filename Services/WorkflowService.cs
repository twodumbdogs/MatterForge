using MatterForge.Data;
using MatterForge.Models;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Services;

public class WorkflowService(MatterForgeDbContext db)
{
    public async Task<SubmissionWorkflowInstance?> EnsureStartedAsync(Guid formSubmissionId)
    {
        var submission = await db.FormSubmissions
            .Include(x => x.FormDefinition)
            .FirstOrDefaultAsync(x => x.Id == formSubmissionId);

        return submission is null ? null : await EnsureStartedAsync(submission);
    }

    public async Task<SubmissionWorkflowInstance?> EnsureStartedAsync(FormSubmission submission)
    {
        if (submission.Status == SubmissionStatuses.Converted)
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

        if (existing is not null)
        {
            return existing;
        }

        var firstStep = GetNextMatchingStep(workflow.Steps, submission, null);
        if (firstStep is null)
        {
            return null;
        }

        var instance = new SubmissionWorkflowInstance
        {
            FormSubmissionId = submission.Id,
            WorkflowDefinitionId = workflow.Id,
            Status = WorkflowStatuses.Active,
            CurrentStepNumber = firstStep.StepNumber
        };

        instance.Tasks.Add(CreateTask(submission.Id, firstStep));
        instance.Events.Add(new SubmissionWorkflowEvent
        {
            FormSubmissionId = submission.Id,
            EventType = WorkflowStatuses.EventStarted,
            Message = $"Started workflow: {workflow.Name}"
        });

        submission.Status = SubmissionStatuses.InReview;

        db.SubmissionWorkflowInstances.Add(instance);
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

        if (nextStep is null)
        {
            instance.Status = WorkflowStatuses.Completed;
            instance.CompletedAt = DateTimeOffset.UtcNow;
            db.SubmissionWorkflowEvents.Add(new SubmissionWorkflowEvent
            {
                SubmissionWorkflowInstanceId = instance.Id,
                FormSubmissionId = task.FormSubmissionId,
                EventType = WorkflowStatuses.EventCompleted,
                Message = $"{workflow.Name} completed",
                ActorUserId = actorUserId
            });
        }
        else
        {
            instance.CurrentStepNumber = nextStep.StepNumber;
            var nextTask = CreateTask(task.FormSubmissionId, nextStep);
            nextTask.SubmissionWorkflowInstanceId = instance.Id;
            db.SubmissionWorkflowTasks.Add(nextTask);
        }

        await db.SaveChangesAsync();
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
