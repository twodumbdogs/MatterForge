using System.Globalization;
using System.Text;
using CMIForge.Data;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public static class ExportDataSets
{
    public const string Clients = "clients";
    public const string Matters = "matters";
    public const string Parties = "parties";
    public const string Contacts = "contacts";
    public const string Users = "users";
    public const string TimeEntries = "time-entries";

    public static readonly IReadOnlyList<ExportDataSetDefinition> All =
    [
        new(Clients, "Clients"),
        new(Matters, "Matters"),
        new(Parties, "Parties"),
        new(Contacts, "Contacts"),
        new(Users, "Users"),
        new(TimeEntries, "Time Entries")
    ];
}

public sealed record ExportDataSetDefinition(string Key, string Name);

public sealed record CsvExportResult(string FileName, byte[] Content);

public class CsvExportService(CMIForgeDbContext db)
{
    public async Task<int> CountAsync(string dataSet)
    {
        return dataSet switch
        {
            ExportDataSets.Clients => await db.Clients.AsNoTracking().CountAsync(),
            ExportDataSets.Matters => await db.Matters.AsNoTracking().CountAsync(),
            ExportDataSets.Parties => await db.Parties.AsNoTracking().CountAsync(),
            ExportDataSets.Contacts => await db.Contacts.AsNoTracking().CountAsync(),
            ExportDataSets.Users => await db.Users.AsNoTracking().CountAsync(),
            ExportDataSets.TimeEntries => await db.TimeEntries.AsNoTracking().CountAsync(),
            _ => throw new InvalidOperationException($"Unsupported export data set '{dataSet}'.")
        };
    }

    public async Task<CsvExportResult> ExportAsync(string dataSet, int batchNumber, int batchSize)
    {
        if (batchNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(batchNumber), "Batch number must be 1 or greater.");
        }

        if (batchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be 1 or greater.");
        }

        var skip = (batchNumber - 1) * batchSize;
        var csv = dataSet switch
        {
            ExportDataSets.Clients => await ExportClientsAsync(skip, batchSize),
            ExportDataSets.Matters => await ExportMattersAsync(skip, batchSize),
            ExportDataSets.Parties => await ExportPartiesAsync(skip, batchSize),
            ExportDataSets.Contacts => await ExportContactsAsync(skip, batchSize),
            ExportDataSets.Users => await ExportUsersAsync(skip, batchSize),
            ExportDataSets.TimeEntries => await ExportTimeEntriesAsync(skip, batchSize),
            _ => throw new InvalidOperationException($"Unsupported export data set '{dataSet}'.")
        };

        var fileName = $"cmiforge-{dataSet}-batch-{batchNumber}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv";
        return new CsvExportResult(fileName, Encoding.UTF8.GetBytes(csv));
    }

    private async Task<string> ExportClientsAsync(int skip, int take)
    {
        var rows = await db.Clients
            .AsNoTracking()
            .OrderBy(x => x.ClientNumber)
            .Skip(skip)
            .Take(take)
            .Select(x => new object?[]
            {
                x.Id,
                x.ClientNumber.ToString("D8"),
                x.Name,
                x.Status,
                x.PrimaryContact,
                x.Email,
                x.Phone,
                x.AddressLine1,
                x.AddressLine2,
                x.City,
                x.State,
                x.PostalCode,
                x.Country,
                x.Notes,
                x.IsArchived,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync();

        return BuildCsv(
            ["Id", "ClientNumber", "Name", "Status", "PrimaryContact", "Email", "Phone", "AddressLine1", "AddressLine2", "City", "State", "PostalCode", "Country", "Notes", "IsArchived", "CreatedAt", "UpdatedAt"],
            rows);
    }

    private async Task<string> ExportMattersAsync(int skip, int take)
    {
        var rows = await db.Matters
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.ResponsibleUser)
            .Include(x => x.LeadPartner)
            .OrderBy(x => x.MatterNumber)
            .Skip(skip)
            .Take(take)
            .Select(x => new object?[]
            {
                x.Id,
                x.MatterNumber.ToString("D8"),
                x.Name,
                x.ClientId,
                x.Client == null ? string.Empty : x.Client.ClientNumber.ToString("D8"),
                x.Client == null ? string.Empty : x.Client.Name,
                x.PracticeArea,
                x.Status,
                x.OpenedDate,
                x.ResponsibleUser == null ? string.Empty : x.ResponsibleUser.DisplayName,
                x.LeadPartner == null ? string.Empty : x.LeadPartner.DisplayName,
                x.RequiresTimeApproval,
                x.TimeIncrementMinutes,
                x.Notes,
                x.IsArchived,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync();

        return BuildCsv(
            ["Id", "MatterNumber", "Name", "ClientId", "ClientNumber", "ClientName", "PracticeArea", "Status", "OpenedDate", "ResponsibleUser", "LeadPartner", "RequiresTimeApproval", "TimeIncrementMinutes", "Notes", "IsArchived", "CreatedAt", "UpdatedAt"],
            rows);
    }

    private async Task<string> ExportPartiesAsync(int skip, int take)
    {
        var rows = await db.Parties
            .AsNoTracking()
            .OrderBy(x => x.PartyNumber)
            .Skip(skip)
            .Take(take)
            .Select(x => new object?[]
            {
                x.Id,
                x.PartyNumber.ToString("D8"),
                x.Name,
                x.NormalizedName,
                x.PartyType,
                x.Status,
                x.Notes,
                x.IsArchived,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync();

        return BuildCsv(
            ["Id", "PartyNumber", "Name", "NormalizedName", "PartyType", "Status", "Notes", "IsArchived", "CreatedAt", "UpdatedAt"],
            rows);
    }

    private async Task<string> ExportContactsAsync(int skip, int take)
    {
        var rows = await db.Contacts
            .AsNoTracking()
            .OrderBy(x => x.ContactNumber)
            .Skip(skip)
            .Take(take)
            .Select(x => new object?[]
            {
                x.Id,
                x.ContactNumber.ToString("D8"),
                x.FirstName,
                x.MiddleName,
                x.LastName,
                x.DisplayName,
                x.Organization,
                x.Title,
                x.Email,
                x.Phone,
                x.MobilePhone,
                x.AddressLine1,
                x.AddressLine2,
                x.City,
                x.State,
                x.PostalCode,
                x.Country,
                x.Notes,
                x.IsArchived,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync();

        return BuildCsv(
            ["Id", "ContactNumber", "FirstName", "MiddleName", "LastName", "DisplayName", "Organization", "Title", "Email", "Phone", "MobilePhone", "AddressLine1", "AddressLine2", "City", "State", "PostalCode", "Country", "Notes", "IsArchived", "CreatedAt", "UpdatedAt"],
            rows);
    }

    private async Task<string> ExportUsersAsync(int skip, int take)
    {
        var rows = await db.Users
            .AsNoTracking()
            .OrderBy(x => x.SystemId)
            .Skip(skip)
            .Take(take)
            .Select(x => new object?[]
            {
                x.Id,
                x.SystemId.ToString("D8"),
                x.FirstName,
                x.MiddleName,
                x.LastName,
                x.DisplayName,
                x.Email,
                x.EntraTenantId,
                x.EntraObjectId,
                x.EntraUserPrincipalName,
                x.Title,
                x.IsActive,
                x.IsArchived,
                x.CreatedAt,
                x.UpdatedAt,
                x.LastLoginAt
            })
            .ToListAsync();

        return BuildCsv(
            ["Id", "SystemId", "FirstName", "MiddleName", "LastName", "DisplayName", "Email", "EntraTenantId", "EntraObjectId", "EntraUserPrincipalName", "Title", "IsActive", "IsArchived", "CreatedAt", "UpdatedAt", "LastLoginAt"],
            rows);
    }

    private async Task<string> ExportTimeEntriesAsync(int skip, int take)
    {
        var rows = await db.TimeEntries
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Client)
            .Include(x => x.Matter)
            .Include(x => x.TimePhase)
            .Include(x => x.TimeTask)
            .Include(x => x.ApprovedByUser)
            .Include(x => x.ExportedByUser)
            .OrderBy(x => x.TimeEntryNumber)
            .Skip(skip)
            .Take(take)
            .Select(x => new object?[]
            {
                x.Id,
                x.TimeEntryNumber.ToString("D8"),
                x.User == null ? string.Empty : x.User.DisplayName,
                x.Client == null ? string.Empty : x.Client.ClientNumber.ToString("D8"),
                x.Client == null ? string.Empty : x.Client.Name,
                x.Matter == null ? string.Empty : x.Matter.MatterNumber.ToString("D8"),
                x.Matter == null ? string.Empty : x.Matter.Name,
                x.TimePhase == null ? string.Empty : x.TimePhase.Code,
                x.TimePhase == null ? string.Empty : x.TimePhase.Name,
                x.TimeTask == null ? string.Empty : x.TimeTask.Code,
                x.TimeTask == null ? string.Empty : x.TimeTask.Name,
                x.WorkDate,
                x.Minutes,
                x.Minutes / 60m,
                x.ClientNarrative,
                x.InternalNotes,
                x.IsBillable,
                x.Status,
                x.SubmittedAt,
                x.ApprovedAt,
                x.ApprovedByUser == null ? string.Empty : x.ApprovedByUser.DisplayName,
                x.ExportedAt,
                x.ExportedByUser == null ? string.Empty : x.ExportedByUser.DisplayName,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync();

        return BuildCsv(
            ["Id", "TimeEntryNumber", "User", "ClientNumber", "ClientName", "MatterNumber", "MatterName", "PhaseCode", "PhaseName", "TaskCode", "TaskName", "WorkDate", "Minutes", "Hours", "ClientNarrative", "InternalNotes", "IsBillable", "Status", "SubmittedAt", "ApprovedAt", "ApprovedByUser", "ExportedAt", "ExportedByUser", "CreatedAt", "UpdatedAt"],
            rows);
    }

    private static string BuildCsv(string[] headers, IEnumerable<object?[]> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", row.Select(x => Escape(FormatValue(x)))));
        }

        return builder.ToString();
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTimeOffset dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "true" : "false",
            decimal number => number.ToString("0.####", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
