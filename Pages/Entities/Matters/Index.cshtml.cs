using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Matters;

public class IndexModel(CMIForgeDbContext db) : PageModel
{
    public List<Matter> Matters { get; private set; } = [];

    public RecordPage Pagination { get; private set; } = RecordPage.Empty;


    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SortColumn { get; set; } = "number";

    [BindProperty(SupportsGet = true)]
    public string SortDirection { get; set; } = ListSort.Ascending;

    public async Task OnGetAsync()
    {
        var query = db.Matters
            .Where(x => !x.IsArchived);

        Search = Search?.Trim();
        var recordNumber = SearchTermParser.TryParseRecordNumber(Search);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(x =>
                (recordNumber.HasValue && x.MatterNumber == recordNumber.Value) ||
                x.Name.Contains(Search) ||
                x.PracticeArea.Contains(Search) ||
                x.Status.Contains(Search) ||
                x.Notes.Contains(Search) ||
                (x.Client != null && x.Client.Name.Contains(Search)) ||
                (x.ResponsibleUser != null && x.ResponsibleUser.DisplayName.Contains(Search)) ||
                (x.LeadPartner != null && x.LeadPartner.DisplayName.Contains(Search)));
        }

        Pagination = RecordPage.Create(PageNumber, await query.CountAsync());
        PageNumber = Pagination.PageNumber;

        SortColumn = NormalizeSortColumn(SortColumn);
        SortDirection = ListSort.NormalizeDirection(SortDirection);

        Matters = await ApplySort(query)
            .Include(x => x.Client)
            .Include(x => x.ResponsibleUser)
            .Include(x => x.LeadPartner)
            .Skip(Pagination.Skip)
            .Take(Pagination.PageSize)
            .ToListAsync();
    }

    public string NextSortDirection(string column) => ListSort.NextDirection(SortColumn, SortDirection, column);

    public Dictionary<string, string> RouteValues => new()
    {
        ["Search"] = Search ?? string.Empty,
        ["SortColumn"] = SortColumn,
        ["SortDirection"] = SortDirection
    };

    private IQueryable<Matter> ApplySort(IQueryable<Matter> query)
    {
        var descending = ListSort.IsDescending(SortDirection);
        return SortColumn switch
        {
            "matter" => descending ? query.OrderByDescending(x => x.Name).ThenBy(x => x.MatterNumber) : query.OrderBy(x => x.Name).ThenBy(x => x.MatterNumber),
            "client" => descending ? query.OrderByDescending(x => x.Client!.Name).ThenBy(x => x.MatterNumber) : query.OrderBy(x => x.Client!.Name).ThenBy(x => x.MatterNumber),
            "practice" => descending ? query.OrderByDescending(x => x.PracticeArea).ThenBy(x => x.MatterNumber) : query.OrderBy(x => x.PracticeArea).ThenBy(x => x.MatterNumber),
            "status" => descending ? query.OrderByDescending(x => x.Status).ThenBy(x => x.MatterNumber) : query.OrderBy(x => x.Status).ThenBy(x => x.MatterNumber),
            "responsible" => descending ? query.OrderByDescending(x => x.ResponsibleUser!.DisplayName).ThenBy(x => x.MatterNumber) : query.OrderBy(x => x.ResponsibleUser!.DisplayName).ThenBy(x => x.MatterNumber),
            "opened" => descending ? query.OrderByDescending(x => x.OpenedDate).ThenBy(x => x.MatterNumber) : query.OrderBy(x => x.OpenedDate).ThenBy(x => x.MatterNumber),
            _ => descending ? query.OrderByDescending(x => x.MatterNumber) : query.OrderBy(x => x.MatterNumber)
        };
    }

    private static string NormalizeSortColumn(string? column)
    {
        return column is "number" or "matter" or "client" or "practice" or "status" or "responsible" or "opened"
            ? column
            : "number";
    }
}
