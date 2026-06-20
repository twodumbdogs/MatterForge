using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Users;

public class IndexModel(CMIForgeDbContext db) : PageModel
{
    public List<CMIForgeUser> Users { get; private set; } = [];

    public RecordPage Pagination { get; private set; } = RecordPage.Empty;


    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SortColumn { get; set; } = "system-id";

    [BindProperty(SupportsGet = true)]
    public string SortDirection { get; set; } = ListSort.Ascending;

    public async Task OnGetAsync()
    {
        var query = db.Users
            .Where(x => !x.IsArchived);

        Search = Search?.Trim();
        var recordNumber = SearchTermParser.TryParseRecordNumber(Search);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(x =>
                (recordNumber.HasValue && x.SystemId == recordNumber.Value) ||
                x.DisplayName.Contains(Search) ||
                x.FirstName.Contains(Search) ||
                x.MiddleName.Contains(Search) ||
                x.LastName.Contains(Search) ||
                x.Email.Contains(Search) ||
                x.EntraUserPrincipalName.Contains(Search) ||
                x.Title.Contains(Search));
        }

        Pagination = RecordPage.Create(PageNumber, await query.CountAsync());
        PageNumber = Pagination.PageNumber;

        SortColumn = NormalizeSortColumn(SortColumn);
        SortDirection = ListSort.NormalizeDirection(SortDirection);

        Users = await ApplySort(query)
            .Include(x => x.ResponsibleMatters)
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

    private IQueryable<CMIForgeUser> ApplySort(IQueryable<CMIForgeUser> query)
    {
        var descending = ListSort.IsDescending(SortDirection);
        return SortColumn switch
        {
            "name" => descending ? query.OrderByDescending(x => x.DisplayName).ThenBy(x => x.SystemId) : query.OrderBy(x => x.DisplayName).ThenBy(x => x.SystemId),
            "first" => descending ? query.OrderByDescending(x => x.FirstName).ThenBy(x => x.SystemId) : query.OrderBy(x => x.FirstName).ThenBy(x => x.SystemId),
            "last" => descending ? query.OrderByDescending(x => x.LastName).ThenBy(x => x.SystemId) : query.OrderBy(x => x.LastName).ThenBy(x => x.SystemId),
            "email" => descending ? query.OrderByDescending(x => x.Email).ThenBy(x => x.SystemId) : query.OrderBy(x => x.Email).ThenBy(x => x.SystemId),
            "title" => descending ? query.OrderByDescending(x => x.Title).ThenBy(x => x.SystemId) : query.OrderBy(x => x.Title).ThenBy(x => x.SystemId),
            "status" => descending ? query.OrderByDescending(x => x.IsActive).ThenBy(x => x.SystemId) : query.OrderBy(x => x.IsActive).ThenBy(x => x.SystemId),
            "last-login" => descending ? query.OrderByDescending(x => x.LastLoginAt).ThenBy(x => x.SystemId) : query.OrderBy(x => x.LastLoginAt).ThenBy(x => x.SystemId),
            "responsible" => descending ? query.OrderByDescending(x => x.ResponsibleMatters.Count).ThenBy(x => x.SystemId) : query.OrderBy(x => x.ResponsibleMatters.Count).ThenBy(x => x.SystemId),
            _ => descending ? query.OrderByDescending(x => x.SystemId) : query.OrderBy(x => x.SystemId)
        };
    }

    private static string NormalizeSortColumn(string? column)
    {
        return column is "system-id" or "name" or "first" or "last" or "email" or "title" or "status" or "last-login" or "responsible"
            ? column
            : "system-id";
    }
}
