namespace MatterForge.Models;

public class ImportBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ImportType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string Status { get; set; } = ImportBatchStatuses.Completed;

    public int TotalRows { get; set; }

    public int ImportedRows { get; set; }

    public int UpdatedRows { get; set; }

    public int SkippedRows { get; set; }

    public int ErrorRows { get; set; }

    public string Summary { get; set; } = string.Empty;

    public Guid? ImportedByUserId { get; set; }

    public MatterForgeUser? ImportedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ImportBatchRow> Rows { get; set; } = [];
}

public static class ImportTypes
{
    public const string Clients = "Clients";
    public const string Matters = "Matters";
    public const string Parties = "Parties";

    public static readonly string[] All = [Clients, Matters, Parties];
}

public static class ImportBatchStatuses
{
    public const string Completed = "Completed";
    public const string CompletedWithErrors = "Completed With Errors";
    public const string Failed = "Failed";
    public const string ValidationPassed = "Validation Passed";
    public const string ValidationIssues = "Validation Issues";
}

public static class ImportRowStatuses
{
    public const string Imported = "Imported";
    public const string Updated = "Updated";
    public const string Skipped = "Skipped";
    public const string Error = "Error";
    public const string WouldImport = "Would Import";
    public const string WouldUpdate = "Would Update";
    public const string WouldSkip = "Would Skip";
}
