namespace CMIForge.Models;

public static class ListSort
{
    public const string Ascending = "asc";
    public const string Descending = "desc";

    public static string NormalizeDirection(string? direction)
    {
        return string.Equals(direction, Descending, StringComparison.OrdinalIgnoreCase)
            ? Descending
            : Ascending;
    }

    public static bool IsDescending(string? direction)
    {
        return NormalizeDirection(direction) == Descending;
    }

    public static string NextDirection(string? currentColumn, string? currentDirection, string column)
    {
        return string.Equals(currentColumn, column, StringComparison.OrdinalIgnoreCase) &&
            NormalizeDirection(currentDirection) == Ascending
                ? Descending
                : Ascending;
    }

    public static string HeaderClass(string? currentColumn, string column)
    {
        return string.Equals(currentColumn, column, StringComparison.OrdinalIgnoreCase)
            ? "mf-sortable-heading mf-sort-active"
            : "mf-sortable-heading";
    }
}
