namespace CMIForge.Services;

public static class RecordNumbers
{
    public static string Submission(int number) => $"S-{number:D8}";

    public static string ConflictSearch(int number) => $"C-{number:D8}";

    public static string TimeEntry(int number) => $"T-{number:D8}";

    public static string Client(int number) => number.ToString("D8");

    public static string Matter(int number) => number.ToString("D8");

    public static string ConflictSearchDisplayName(string searchName, int? submissionNumber, int? matterNumber)
    {
        if (submissionNumber.HasValue && !matterNumber.HasValue)
        {
            return $"{Submission(submissionNumber.Value)} Search";
        }

        return searchName;
    }
}
