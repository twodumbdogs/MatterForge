namespace MatterForge.Models;

public class ImportBatchRow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ImportBatchId { get; set; }

    public ImportBatch? ImportBatch { get; set; }

    public int RowNumber { get; set; }

    public string Status { get; set; } = ImportRowStatuses.Imported;

    public string Message { get; set; } = string.Empty;

    public string SourceJson { get; set; } = string.Empty;
}
