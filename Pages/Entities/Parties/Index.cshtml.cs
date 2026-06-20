using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Parties;

public class IndexModel(CMIForgeDbContext db) : PageModel
{
    public List<Party> Parties { get; private set; } = [];

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
        var query = db.Parties
            .Where(x => !x.IsArchived);

        Search = Search?.Trim();
        var recordNumber = SearchTermParser.TryParseRecordNumber(Search);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(x =>
                (recordNumber.HasValue && x.PartyNumber == recordNumber.Value) ||
                x.Name.Contains(Search) ||
                x.NormalizedName.Contains(Search) ||
                x.PartyType.Contains(Search) ||
                x.Status.Contains(Search) ||
                x.Notes.Contains(Search) ||
                x.Aliases.Any(alias =>
                    alias.Alias.Contains(Search) ||
                    alias.NormalizedAlias.Contains(Search) ||
                    alias.Notes.Contains(Search)));
        }

        Pagination = RecordPage.Create(PageNumber, await query.CountAsync());
        PageNumber = Pagination.PageNumber;

        SortColumn = NormalizeSortColumn(SortColumn);
        SortDirection = ListSort.NormalizeDirection(SortDirection);

        Parties = await ApplySort(query)
            .Include(x => x.Aliases)
            .Include(x => x.MatterParties)
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

    private IQueryable<Party> ApplySort(IQueryable<Party> query)
    {
        var descending = ListSort.IsDescending(SortDirection);
        return SortColumn switch
        {
            "party" => descending ? query.OrderByDescending(x => x.Name).ThenBy(x => x.PartyNumber) : query.OrderBy(x => x.Name).ThenBy(x => x.PartyNumber),
            "type" => descending ? query.OrderByDescending(x => x.PartyType).ThenBy(x => x.PartyNumber) : query.OrderBy(x => x.PartyType).ThenBy(x => x.PartyNumber),
            "status" => descending ? query.OrderByDescending(x => x.Status).ThenBy(x => x.PartyNumber) : query.OrderBy(x => x.Status).ThenBy(x => x.PartyNumber),
            "aliases" => descending ? query.OrderByDescending(x => x.Aliases.Count).ThenBy(x => x.PartyNumber) : query.OrderBy(x => x.Aliases.Count).ThenBy(x => x.PartyNumber),
            "matter-links" => descending ? query.OrderByDescending(x => x.MatterParties.Count).ThenBy(x => x.PartyNumber) : query.OrderBy(x => x.MatterParties.Count).ThenBy(x => x.PartyNumber),
            _ => descending ? query.OrderByDescending(x => x.PartyNumber) : query.OrderBy(x => x.PartyNumber)
        };
    }

    private static string NormalizeSortColumn(string? column)
    {
        return column is "number" or "party" or "type" or "status" or "aliases" or "matter-links"
            ? column
            : "number";
    }
}
