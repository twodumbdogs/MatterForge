namespace CMIForge.Models;

public sealed class RecordPage
{
    public const int DefaultPageSize = 50;

    public static RecordPage Empty { get; } = new(1, DefaultPageSize, 0);

    private RecordPage(int pageNumber, int pageSize, int totalRecords)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalRecords = totalRecords;
    }

    public int PageNumber { get; }

    public int PageSize { get; }

    public int TotalRecords { get; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));

    public int Skip => (PageNumber - 1) * PageSize;

    public int FirstRecord => TotalRecords == 0 ? 0 : Skip + 1;

    public int LastRecord => Math.Min(Skip + PageSize, TotalRecords);

    public bool HasPrevious => PageNumber > 1;

    public bool HasNext => PageNumber < TotalPages;

    public int PreviousPage => HasPrevious ? PageNumber - 1 : PageNumber;

    public int NextPage => HasNext ? PageNumber + 1 : PageNumber;

    public static RecordPage Create(int requestedPageNumber, int totalRecords, int pageSize = DefaultPageSize)
    {
        pageSize = pageSize < 1 ? DefaultPageSize : pageSize;
        totalRecords = Math.Max(0, totalRecords);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
        var pageNumber = Math.Clamp(requestedPageNumber < 1 ? 1 : requestedPageNumber, 1, totalPages);

        return new RecordPage(pageNumber, pageSize, totalRecords);
    }
}
