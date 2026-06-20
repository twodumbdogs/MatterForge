namespace CMIForge.Models;

public sealed record ListSearchViewModel(
    string? Search,
    string Placeholder,
    string Label = "Search",
    string PageName = "./Index",
    string QueryParameter = "Search",
    IDictionary<string, string>? RouteValues = null);
