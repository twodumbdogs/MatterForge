using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Clients;

public class IndexModel(CMIForgeDbContext db) : PageModel
{
    public List<Client> Clients { get; private set; } = [];

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
        var query = db.Clients
            .Include(x => x.Aliases)
            .Where(x => !x.IsArchived);

        Search = Search?.Trim();
        var recordNumber = SearchTermParser.TryParseRecordNumber(Search);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(x =>
                (recordNumber.HasValue && x.ClientNumber == recordNumber.Value) ||
                x.Name.Contains(Search) ||
                x.Status.Contains(Search) ||
                x.PrimaryContact.Contains(Search) ||
                x.Email.Contains(Search) ||
                x.Phone.Contains(Search) ||
                x.AddressLine1.Contains(Search) ||
                x.AddressLine2.Contains(Search) ||
                x.City.Contains(Search) ||
                x.State.Contains(Search) ||
                x.PostalCode.Contains(Search) ||
                x.Country.Contains(Search) ||
                x.Notes.Contains(Search) ||
                x.Aliases.Any(alias =>
                    alias.Alias.Contains(Search) ||
                    alias.NormalizedAlias.Contains(Search)));
        }

        Pagination = RecordPage.Create(PageNumber, await query.CountAsync());
        PageNumber = Pagination.PageNumber;

        SortColumn = NormalizeSortColumn(SortColumn);
        SortDirection = ListSort.NormalizeDirection(SortDirection);

        Clients = await ApplySort(query)
            .Include(x => x.Matters)
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

    private IQueryable<Client> ApplySort(IQueryable<Client> query)
    {
        var descending = ListSort.IsDescending(SortDirection);
        return SortColumn switch
        {
            "name" => descending ? query.OrderByDescending(x => x.Name).ThenBy(x => x.ClientNumber) : query.OrderBy(x => x.Name).ThenBy(x => x.ClientNumber),
            "status" => descending ? query.OrderByDescending(x => x.Status).ThenBy(x => x.ClientNumber) : query.OrderBy(x => x.Status).ThenBy(x => x.ClientNumber),
            "primary" => descending ? query.OrderByDescending(x => x.PrimaryContact).ThenBy(x => x.ClientNumber) : query.OrderBy(x => x.PrimaryContact).ThenBy(x => x.ClientNumber),
            "email" => descending ? query.OrderByDescending(x => x.Email).ThenBy(x => x.ClientNumber) : query.OrderBy(x => x.Email).ThenBy(x => x.ClientNumber),
            "aliases" => descending ? query.OrderByDescending(x => x.Aliases.Count).ThenBy(x => x.ClientNumber) : query.OrderBy(x => x.Aliases.Count).ThenBy(x => x.ClientNumber),
            "matters" => descending ? query.OrderByDescending(x => x.Matters.Count).ThenBy(x => x.ClientNumber) : query.OrderBy(x => x.Matters.Count).ThenBy(x => x.ClientNumber),
            _ => descending ? query.OrderByDescending(x => x.ClientNumber) : query.OrderBy(x => x.ClientNumber)
        };
    }

    private static string NormalizeSortColumn(string? column)
    {
        return column is "number" or "name" or "status" or "primary" or "email" or "aliases" or "matters"
            ? column
            : "number";
    }
}
