using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Entities.Contacts;

public class IndexModel(CMIForgeDbContext db) : PageModel
{
    public List<Contact> Contacts { get; private set; } = [];

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
        var query = db.Contacts
            .Where(x => !x.IsArchived);

        Search = Search?.Trim();
        var recordNumber = SearchTermParser.TryParseRecordNumber(Search);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(x =>
                (recordNumber.HasValue && x.ContactNumber == recordNumber.Value) ||
                x.DisplayName.Contains(Search) ||
                x.FirstName.Contains(Search) ||
                x.MiddleName.Contains(Search) ||
                x.LastName.Contains(Search) ||
                x.Organization.Contains(Search) ||
                x.Title.Contains(Search) ||
                x.Email.Contains(Search) ||
                x.Phone.Contains(Search) ||
                x.MobilePhone.Contains(Search) ||
                x.AddressLine1.Contains(Search) ||
                x.AddressLine2.Contains(Search) ||
                x.City.Contains(Search) ||
                x.State.Contains(Search) ||
                x.PostalCode.Contains(Search) ||
                x.Country.Contains(Search) ||
                x.Notes.Contains(Search));
        }

        Pagination = RecordPage.Create(PageNumber, await query.CountAsync());
        PageNumber = Pagination.PageNumber;

        SortColumn = NormalizeSortColumn(SortColumn);
        SortDirection = ListSort.NormalizeDirection(SortDirection);

        Contacts = await ApplySort(query)
            .Include(x => x.ClientLinks)
            .Include(x => x.MatterLinks)
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

    private IQueryable<Contact> ApplySort(IQueryable<Contact> query)
    {
        var descending = ListSort.IsDescending(SortDirection);
        return SortColumn switch
        {
            "name" => descending ? query.OrderByDescending(x => x.DisplayName).ThenBy(x => x.ContactNumber) : query.OrderBy(x => x.DisplayName).ThenBy(x => x.ContactNumber),
            "organization" => descending ? query.OrderByDescending(x => x.Organization).ThenBy(x => x.ContactNumber) : query.OrderBy(x => x.Organization).ThenBy(x => x.ContactNumber),
            "email" => descending ? query.OrderByDescending(x => x.Email).ThenBy(x => x.ContactNumber) : query.OrderBy(x => x.Email).ThenBy(x => x.ContactNumber),
            "phone" => descending ? query.OrderByDescending(x => x.Phone).ThenBy(x => x.ContactNumber) : query.OrderBy(x => x.Phone).ThenBy(x => x.ContactNumber),
            "client-links" => descending ? query.OrderByDescending(x => x.ClientLinks.Count).ThenBy(x => x.ContactNumber) : query.OrderBy(x => x.ClientLinks.Count).ThenBy(x => x.ContactNumber),
            "matter-links" => descending ? query.OrderByDescending(x => x.MatterLinks.Count).ThenBy(x => x.ContactNumber) : query.OrderBy(x => x.MatterLinks.Count).ThenBy(x => x.ContactNumber),
            _ => descending ? query.OrderByDescending(x => x.ContactNumber) : query.OrderBy(x => x.ContactNumber)
        };
    }

    private static string NormalizeSortColumn(string? column)
    {
        return column is "number" or "name" or "organization" or "email" or "phone" or "client-links" or "matter-links"
            ? column
            : "number";
    }
}
