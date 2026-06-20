using CMIForge.Models;

namespace CMIForge.Services;

public static class WorkflowStepConditionOperators
{
    public const string Always = "Always";
    public const string Equal = "Equals";
    public const string NotEquals = "Not Equals";
    public const string Contains = "Contains";
    public const string IsPresent = "Is Present";
    public const string IsBlank = "Is Blank";

    public static readonly string[] All = [Always, Equal, NotEquals, Contains, IsPresent, IsBlank];

    public static bool IsValid(string? value)
    {
        return All.Contains(value, StringComparer.OrdinalIgnoreCase);
    }
}

public static class WorkflowStepConditions
{
    public static bool Matches(WorkflowStep step, FormSubmission submission)
    {
        if (string.IsNullOrWhiteSpace(step.ConditionFieldKey) ||
            string.IsNullOrWhiteSpace(step.ConditionOperator) ||
            step.ConditionOperator.Equals(WorkflowStepConditionOperators.Always, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var answers = SubmissionAnswerReader.Read(submission.DataJson);
        var key = SubmissionAnswerReader.NormalizeKey(step.ConditionFieldKey);
        answers.TryGetValue(key, out var answer);
        answer ??= string.Empty;

        return step.ConditionOperator switch
        {
            var x when x.Equals(WorkflowStepConditionOperators.Equal, StringComparison.OrdinalIgnoreCase) =>
                answer.Equals(step.ConditionValue ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            var x when x.Equals(WorkflowStepConditionOperators.NotEquals, StringComparison.OrdinalIgnoreCase) =>
                !answer.Equals(step.ConditionValue ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            var x when x.Equals(WorkflowStepConditionOperators.Contains, StringComparison.OrdinalIgnoreCase) =>
                answer.Contains(step.ConditionValue ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            var x when x.Equals(WorkflowStepConditionOperators.IsPresent, StringComparison.OrdinalIgnoreCase) =>
                !string.IsNullOrWhiteSpace(answer),
            var x when x.Equals(WorkflowStepConditionOperators.IsBlank, StringComparison.OrdinalIgnoreCase) =>
                string.IsNullOrWhiteSpace(answer),
            _ => true
        };
    }
}
