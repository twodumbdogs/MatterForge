namespace MatterForge.Services;

public static class RecordNumbers
{
    public static string Submission(int number) => $"S-{number:D8}";

    public static string ConflictSearch(int number) => $"C-{number:D8}";

    public static string Client(int number) => number.ToString("D8");

    public static string Matter(int number) => number.ToString("D8");
}
