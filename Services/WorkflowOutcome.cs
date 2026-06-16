using System.Text.Json;
using MatterForge.Models;

namespace MatterForge.Services;

public static class WorkflowOutcomeActions
{
    public const string Advance = "Advance";
    public const string Complete = "Complete";
    public const string Return = "Return";

    public static readonly string[] All = [Advance, Complete, Return];

    public static bool IsValid(string? action)
    {
        return All.Contains(action, StringComparer.OrdinalIgnoreCase);
    }
}

public class WorkflowOutcome
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Action { get; set; } = WorkflowOutcomeActions.Advance;

    public string SubmissionStatus { get; set; } = SubmissionStatuses.InReview;

    public int? NextStepNumber { get; set; }
}

public static class WorkflowOutcomeParser
{
    public const string DesignerHelpText = "Label|Action|Status|Next step, separated by semicolons. Actions: Advance, Complete, Return.";

    public static List<WorkflowOutcome> GetOutcomes(WorkflowStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.OutcomesJson))
        {
            try
            {
                var outcomes = JsonSerializer.Deserialize<List<WorkflowOutcome>>(step.OutcomesJson, FormJson.Options) ?? [];
                outcomes = outcomes.Where(IsUsable).Select(Normalize).ToList();
                if (outcomes.Count > 0)
                {
                    return outcomes;
                }
            }
            catch (JsonException)
            {
            }
        }

        return DefaultOutcomes(step.ApprovalLabel, step.CompletionSubmissionStatus);
    }

    public static string Serialize(IEnumerable<WorkflowOutcome> outcomes)
    {
        var normalized = outcomes.Where(IsUsable).Select(Normalize).ToList();
        return JsonSerializer.Serialize(normalized, FormJson.Options);
    }

    public static string ToDesignerText(WorkflowStep step)
    {
        return ToDesignerText(GetOutcomes(step));
    }

    public static string ToDesignerText(IEnumerable<WorkflowOutcome> outcomes)
    {
        return string.Join("; ", outcomes.Select(x =>
        {
            var nextStep = x.NextStepNumber.HasValue ? $"|{x.NextStepNumber.Value}" : string.Empty;
            return $"{x.Label}|{x.Action}|{x.SubmissionStatus}{nextStep}";
        }));
    }

    public static List<WorkflowOutcome> FromDesignerText(string? designerText, string? approvalLabel, string? completionSubmissionStatus)
    {
        if (string.IsNullOrWhiteSpace(designerText))
        {
            return DefaultOutcomes(approvalLabel, completionSubmissionStatus);
        }

        var outcomes = new List<WorkflowOutcome>();
        foreach (var rawOutcome in designerText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = rawOutcome.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
            {
                continue;
            }

            var action = parts.Length > 1 && WorkflowOutcomeActions.IsValid(parts[1])
                ? WorkflowOutcomeActions.All.First(x => x.Equals(parts[1], StringComparison.OrdinalIgnoreCase))
                : WorkflowOutcomeActions.Advance;

            var status = parts.Length > 2 && SubmissionStatuses.IsValid(parts[2])
                ? SubmissionStatuses.All.First(x => x.Equals(parts[2], StringComparison.OrdinalIgnoreCase))
                : string.IsNullOrWhiteSpace(completionSubmissionStatus)
                    ? SubmissionStatuses.InReview
                    : completionSubmissionStatus;

            int? nextStepNumber = null;
            if (parts.Length > 3 && int.TryParse(parts[3], out var parsedNextStepNumber) && parsedNextStepNumber > 0)
            {
                nextStepNumber = parsedNextStepNumber;
            }

            outcomes.Add(new WorkflowOutcome
            {
                Label = parts[0],
                Action = action,
                SubmissionStatus = status,
                NextStepNumber = nextStepNumber
            });
        }

        return outcomes.Count == 0
            ? DefaultOutcomes(approvalLabel, completionSubmissionStatus)
            : outcomes.Select(Normalize).ToList();
    }

    private static List<WorkflowOutcome> DefaultOutcomes(string? approvalLabel, string? completionSubmissionStatus)
    {
        return
        [
            Normalize(new WorkflowOutcome
            {
                Key = "primary",
                Label = string.IsNullOrWhiteSpace(approvalLabel) ? "Approve" : approvalLabel,
                Action = WorkflowOutcomeActions.Advance,
                SubmissionStatus = string.IsNullOrWhiteSpace(completionSubmissionStatus)
                    ? SubmissionStatuses.InReview
                    : completionSubmissionStatus
            }),
            Normalize(new WorkflowOutcome
            {
                Key = "return",
                Label = "Return",
                Action = WorkflowOutcomeActions.Return,
                SubmissionStatus = SubmissionStatuses.Returned
            })
        ];
    }

    private static WorkflowOutcome Normalize(WorkflowOutcome outcome)
    {
        outcome.Label = string.IsNullOrWhiteSpace(outcome.Label) ? "Approve" : outcome.Label.Trim();
        outcome.Action = WorkflowOutcomeActions.IsValid(outcome.Action) ? outcome.Action.Trim() : WorkflowOutcomeActions.Advance;
        outcome.SubmissionStatus = SubmissionStatuses.IsValid(outcome.SubmissionStatus)
            ? SubmissionStatuses.All.First(x => x.Equals(outcome.SubmissionStatus, StringComparison.OrdinalIgnoreCase))
            : SubmissionStatuses.InReview;
        outcome.Key = string.IsNullOrWhiteSpace(outcome.Key) ? KeyFromLabel(outcome.Label) : KeyFromLabel(outcome.Key);
        return outcome;
    }

    private static bool IsUsable(WorkflowOutcome outcome)
    {
        return !string.IsNullOrWhiteSpace(outcome.Label);
    }

    private static string KeyFromLabel(string value)
    {
        var normalized = new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return string.IsNullOrWhiteSpace(normalized) ? "outcome" : normalized;
    }
}
