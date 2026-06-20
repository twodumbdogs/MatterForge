namespace CMIForge.Pages.System;

public sealed record ArchiveRowViewModel(Guid Id, string Number, string Name, string Detail);

public sealed record ArchiveSectionViewModel(string Title, string EntityType, List<ArchiveRowViewModel> Rows);