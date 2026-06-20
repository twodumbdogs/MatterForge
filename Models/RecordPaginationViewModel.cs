namespace CMIForge.Models;

public sealed record RecordPaginationViewModel(
    RecordPage Page,
    string Label,
    IDictionary<string, string>? RouteValues = null,
    string PageName = "./Index");
