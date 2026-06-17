using System.Text;
using System.Text.Json;
using MatterForge.Data;
using MatterForge.Models;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Services;

public class CsvImportService(MatterForgeDbContext db, ProductPlanService productPlanService)
{
    private static readonly JsonSerializerOptions SourceJsonOptions = new() { WriteIndented = false };

    public Task<ImportBatch> ValidateClientsAsync(Stream stream, string fileName, Guid? importedByUserId)
    {
        return ImportClientsAsync(stream, fileName, importedByUserId, validateOnly: true);
    }

    public Task<ImportBatch> ValidateMattersAsync(Stream stream, string fileName, Guid? importedByUserId)
    {
        return ImportMattersAsync(stream, fileName, importedByUserId, validateOnly: true);
    }

    public Task<ImportBatch> ValidatePartiesAsync(Stream stream, string fileName, Guid? importedByUserId)
    {
        return ImportPartiesAsync(stream, fileName, importedByUserId, validateOnly: true);
    }

    public async Task<ImportBatch> ImportClientsAsync(Stream stream, string fileName, Guid? importedByUserId, bool validateOnly = false)
    {
        var batch = CreateBatch(ImportTypes.Clients, fileName, importedByUserId, validateOnly);
        db.ImportBatches.Add(batch);

        var rows = await CsvTable.ReadAsync(stream);
        var requiredHeaders = new[] { "ClientNumber", "ClientName" };
        if (!ValidateHeaders(batch, rows, requiredHeaders))
        {
            await db.SaveChangesAsync();
            return batch;
        }

        var currentClientCount = await db.Clients.CountAsync();
        var clientLimit = productPlanService.CurrentPlan.ClientLimit;
        var seenNumbers = new HashSet<int>();
        foreach (var row in rows.Rows)
        {
            batch.TotalRows++;
            if (!TryReadNumber(row, "ClientNumber", out var clientNumber, out var numberError))
            {
                AddRow(batch, row, ImportRowStatuses.Error, numberError);
                continue;
            }

            if (!seenNumbers.Add(clientNumber))
            {
                AddRow(batch, row, ImportRowStatuses.Error, $"ClientNumber {clientNumber:D8} appears more than once in this file.");
                continue;
            }

            var clientName = row.Value("ClientName");
            if (string.IsNullOrWhiteSpace(clientName))
            {
                AddRow(batch, row, ImportRowStatuses.Error, "ClientName is required.");
                continue;
            }

            var client = await db.Clients.FirstOrDefaultAsync(x => x.ClientNumber == clientNumber);
            var status = DefaultIfBlank(row.Value("Status"), "Active");
            if (client is null)
            {
                if (clientLimit.HasValue && currentClientCount >= clientLimit.Value)
                {
                    AddRow(batch, row, ImportRowStatuses.Error, $"The current plan is limited to {clientLimit.Value:N0} clients.");
                    continue;
                }

                currentClientCount++;
                batch.ImportedRows++;
                if (validateOnly)
                {
                    AddRow(batch, row, ImportRowStatuses.WouldImport, $"Ready to create client {clientNumber:D8}.");
                    continue;
                }

                client = new Client
                {
                    ClientNumber = clientNumber
                };
                db.Clients.Add(client);
                AddRow(batch, row, ImportRowStatuses.Imported, $"Created client {clientNumber:D8}.");
            }
            else
            {
                batch.UpdatedRows++;
                if (validateOnly)
                {
                    AddRow(batch, row, ImportRowStatuses.WouldUpdate, $"Ready to update client {clientNumber:D8}.");
                    continue;
                }

                client.UpdatedAt = DateTimeOffset.UtcNow;
                AddRow(batch, row, ImportRowStatuses.Updated, $"Updated client {clientNumber:D8}.");
            }

            client.Name = clientName.Trim();
            client.Status = status;
            client.PrimaryContact = row.Value("PrimaryContact").Trim();
            client.Email = row.Value("Email").Trim();
            client.Phone = row.Value("Phone").Trim();
            client.Notes = AppendImportNote(client.Notes, batch);
        }

        CompleteBatch(batch, validateOnly);
        await db.SaveChangesAsync();
        return batch;
    }

    public async Task<ImportBatch> ImportMattersAsync(Stream stream, string fileName, Guid? importedByUserId, bool validateOnly = false)
    {
        var batch = CreateBatch(ImportTypes.Matters, fileName, importedByUserId, validateOnly);
        db.ImportBatches.Add(batch);

        var rows = await CsvTable.ReadAsync(stream);
        var requiredHeaders = new[] { "MatterNumber", "MatterName", "ClientNumber" };
        if (!ValidateHeaders(batch, rows, requiredHeaders))
        {
            await db.SaveChangesAsync();
            return batch;
        }

        var users = await db.Users.Where(x => x.IsActive).ToListAsync();
        var currentMatterCount = await db.Matters.CountAsync();
        var matterLimit = productPlanService.CurrentPlan.MatterLimit;
        var seenNumbers = new HashSet<int>();

        foreach (var row in rows.Rows)
        {
            batch.TotalRows++;
            if (!TryReadNumber(row, "MatterNumber", out var matterNumber, out var matterNumberError))
            {
                AddRow(batch, row, ImportRowStatuses.Error, matterNumberError);
                continue;
            }

            if (!seenNumbers.Add(matterNumber))
            {
                AddRow(batch, row, ImportRowStatuses.Error, $"MatterNumber {matterNumber:D8} appears more than once in this file.");
                continue;
            }

            if (!TryReadNumber(row, "ClientNumber", out var clientNumber, out var clientNumberError))
            {
                AddRow(batch, row, ImportRowStatuses.Error, clientNumberError);
                continue;
            }

            var matterName = row.Value("MatterName");
            if (string.IsNullOrWhiteSpace(matterName))
            {
                AddRow(batch, row, ImportRowStatuses.Error, "MatterName is required.");
                continue;
            }

            var client = await db.Clients.FirstOrDefaultAsync(x => x.ClientNumber == clientNumber);
            if (client is null)
            {
                AddRow(batch, row, ImportRowStatuses.Error, $"ClientNumber {clientNumber:D8} does not exist. Import clients first.");
                continue;
            }

            var responsibleAttorney = row.Value("ResponsibleAttorney");
            var responsibleUser = FindUser(users, responsibleAttorney);
            if (!string.IsNullOrWhiteSpace(responsibleAttorney) && responsibleUser is null)
            {
                AddRow(batch, row, ImportRowStatuses.Error, $"ResponsibleAttorney '{responsibleAttorney}' did not match an active user display name or email.");
                continue;
            }

            var matter = await db.Matters.FirstOrDefaultAsync(x => x.MatterNumber == matterNumber);
            var status = DefaultIfBlank(row.Value("Status"), "Open");
            if (matter is null)
            {
                if (matterLimit.HasValue && currentMatterCount >= matterLimit.Value)
                {
                    AddRow(batch, row, ImportRowStatuses.Error, $"The current plan is limited to {matterLimit.Value:N0} matters.");
                    continue;
                }

                currentMatterCount++;
                batch.ImportedRows++;
                if (validateOnly)
                {
                    AddRow(batch, row, ImportRowStatuses.WouldImport, $"Ready to create matter {matterNumber:D8}.");
                    continue;
                }

                matter = new Matter
                {
                    MatterNumber = matterNumber
                };
                db.Matters.Add(matter);
                AddRow(batch, row, ImportRowStatuses.Imported, $"Created matter {matterNumber:D8}.");
            }
            else
            {
                batch.UpdatedRows++;
                if (validateOnly)
                {
                    AddRow(batch, row, ImportRowStatuses.WouldUpdate, $"Ready to update matter {matterNumber:D8}.");
                    continue;
                }

                matter.UpdatedAt = DateTimeOffset.UtcNow;
                AddRow(batch, row, ImportRowStatuses.Updated, $"Updated matter {matterNumber:D8}.");
            }

            matter.Name = matterName.Trim();
            matter.ClientId = client.Id;
            matter.Status = status;
            matter.ResponsibleUserId = responsibleUser?.Id;
            matter.PracticeArea = DefaultIfBlank(row.Value("PracticeArea"), matter.PracticeArea);
            matter.Notes = AppendImportNote(matter.Notes, batch);
        }

        CompleteBatch(batch, validateOnly);
        await db.SaveChangesAsync();
        return batch;
    }

    public async Task<ImportBatch> ImportPartiesAsync(Stream stream, string fileName, Guid? importedByUserId, bool validateOnly = false)
    {
        var batch = CreateBatch(ImportTypes.Parties, fileName, importedByUserId, validateOnly);
        db.ImportBatches.Add(batch);

        var rows = await CsvTable.ReadAsync(stream);
        var requiredHeaders = new[] { "PartyName", "Role", "MatterNumber" };
        if (!ValidateHeaders(batch, rows, requiredHeaders))
        {
            await db.SaveChangesAsync();
            return batch;
        }

        var conflictSearchService = new ConflictSearchService(db);
        var seenLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validatedNewPartyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Rows)
        {
            batch.TotalRows++;
            if (!TryReadNumber(row, "MatterNumber", out var matterNumber, out var matterNumberError))
            {
                AddRow(batch, row, ImportRowStatuses.Error, matterNumberError);
                continue;
            }

            var matter = await db.Matters.FirstOrDefaultAsync(x => x.MatterNumber == matterNumber);
            if (matter is null)
            {
                AddRow(batch, row, ImportRowStatuses.Error, $"MatterNumber {matterNumber:D8} does not exist. Import matters first.");
                continue;
            }

            var partyName = row.Value("PartyName");
            if (string.IsNullOrWhiteSpace(partyName))
            {
                AddRow(batch, row, ImportRowStatuses.Error, "PartyName is required.");
                continue;
            }

            var role = NormalizePartyRole(row.Value("Role"));
            if (string.IsNullOrWhiteSpace(role))
            {
                AddRow(batch, row, ImportRowStatuses.Error, $"Role '{row.Value("Role")}' is not recognized.");
                continue;
            }

            var normalizedName = ConflictSearchService.NormalizeName(partyName);
            var linkKey = $"{matterNumber}:{normalizedName}:{role}";
            if (!seenLinks.Add(linkKey))
            {
                AddRow(batch, row, ImportRowStatuses.Error, $"Party '{partyName}' with role '{role}' appears more than once for matter {matterNumber:D8} in this file.");
                continue;
            }

            var existed = await db.Parties.AnyAsync(x => x.NormalizedName == normalizedName) ||
                db.Parties.Local.Any(x => x.NormalizedName == normalizedName) ||
                validatedNewPartyNames.Contains(normalizedName);

            Party? party = null;
            if (!validateOnly)
            {
                party = await conflictSearchService.GetOrCreatePartyAsync(partyName.Trim());
            }

            var linkExists = await db.MatterParties.AnyAsync(x =>
                    x.MatterId == matter.Id &&
                    x.Party!.NormalizedName == normalizedName &&
                    x.Role == role) ||
                db.MatterParties.Local.Any(x =>
                    x.MatterId == matter.Id &&
                    x.Party?.NormalizedName == normalizedName &&
                    x.Role == role);

            if (linkExists)
            {
                batch.SkippedRows++;
                AddRow(
                    batch,
                    row,
                    validateOnly ? ImportRowStatuses.WouldSkip : ImportRowStatuses.Skipped,
                    $"Matter {matterNumber:D8} already has party '{partyName}' as '{role}'.");
                continue;
            }

            if (validateOnly)
            {
                if (existed)
                {
                    batch.UpdatedRows++;
                    AddRow(batch, row, ImportRowStatuses.WouldUpdate, $"Ready to link existing party '{partyName}' to matter {matterNumber:D8} as '{role}'.");
                }
                else
                {
                    validatedNewPartyNames.Add(normalizedName);
                    batch.ImportedRows++;
                    AddRow(batch, row, ImportRowStatuses.WouldImport, $"Ready to create party '{partyName}' and link it to matter {matterNumber:D8} as '{role}'.");
                }

                continue;
            }

            if (party is null)
            {
                AddRow(batch, row, ImportRowStatuses.Error, $"Party '{partyName}' could not be prepared for import.");
                continue;
            }

            db.MatterParties.Add(new MatterParty
            {
                MatterId = matter.Id,
                Party = party,
                Role = role,
                Notes = $"Imported from {batch.FileName}"
            });

            if (existed)
            {
                batch.UpdatedRows++;
                AddRow(batch, row, ImportRowStatuses.Updated, $"Linked existing party '{partyName}' to matter {matterNumber:D8} as '{role}'.");
            }
            else
            {
                batch.ImportedRows++;
                AddRow(batch, row, ImportRowStatuses.Imported, $"Created party '{partyName}' and linked it to matter {matterNumber:D8} as '{role}'.");
            }
        }

        CompleteBatch(batch, validateOnly);
        await db.SaveChangesAsync();
        return batch;
    }

    private static ImportBatch CreateBatch(string importType, string fileName, Guid? importedByUserId, bool validateOnly)
    {
        return new ImportBatch
        {
            ImportType = importType,
            FileName = string.IsNullOrWhiteSpace(fileName) ? "upload.csv" : Path.GetFileName(fileName),
            ImportedByUserId = importedByUserId,
            Status = validateOnly ? ImportBatchStatuses.ValidationPassed : ImportBatchStatuses.Completed
        };
    }

    private static bool ValidateHeaders(ImportBatch batch, CsvTable table, string[] requiredHeaders)
    {
        if (table.Headers.Count == 0)
        {
            batch.Status = ImportBatchStatuses.Failed;
            batch.ErrorRows = 1;
            batch.Summary = "The CSV file is empty or missing a header row.";
            batch.Rows.Add(new ImportBatchRow
            {
                RowNumber = 1,
                Status = ImportRowStatuses.Error,
                Message = batch.Summary
            });
            return false;
        }

        var missing = requiredHeaders.Where(x => !table.HasHeader(x)).ToList();
        if (missing.Count == 0)
        {
            return true;
        }

        batch.Status = ImportBatchStatuses.Failed;
        batch.ErrorRows = 1;
        batch.Summary = $"Missing required column(s): {string.Join(", ", missing)}.";
        batch.Rows.Add(new ImportBatchRow
        {
            RowNumber = 1,
            Status = ImportRowStatuses.Error,
            Message = batch.Summary
        });
        return false;
    }

    private static void AddRow(ImportBatch batch, CsvRow row, string status, string message)
    {
        if (status == ImportRowStatuses.Error)
        {
            batch.ErrorRows++;
        }

        batch.Rows.Add(new ImportBatchRow
        {
            RowNumber = row.RowNumber,
            Status = status,
            Message = message,
            SourceJson = JsonSerializer.Serialize(row.Values, SourceJsonOptions)
        });
    }

    private static void CompleteBatch(ImportBatch batch, bool validateOnly)
    {
        batch.CompletedAt = DateTimeOffset.UtcNow;
        if (validateOnly)
        {
            batch.Status = batch.ErrorRows > 0
                ? ImportBatchStatuses.ValidationIssues
                : ImportBatchStatuses.ValidationPassed;
            batch.Summary = batch.ErrorRows > 0
                ? $"{batch.ErrorRows:N0} issue(s) found. {batch.ImportedRows:N0} would import, {batch.UpdatedRows:N0} would update, {batch.SkippedRows:N0} would skip."
                : $"Validation passed. {batch.ImportedRows:N0} would import, {batch.UpdatedRows:N0} would update, {batch.SkippedRows:N0} would skip.";
            return;
        }

        batch.Status = batch.ErrorRows > 0
            ? ImportBatchStatuses.CompletedWithErrors
            : ImportBatchStatuses.Completed;
        batch.Summary = $"{batch.ImportedRows:N0} imported, {batch.UpdatedRows:N0} updated, {batch.SkippedRows:N0} skipped, {batch.ErrorRows:N0} errors.";
    }

    private static bool TryReadNumber(CsvRow row, string header, out int number, out string error)
    {
        number = 0;
        var value = row.Value(header);
        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"{header} is required.";
            return false;
        }

        value = value.Trim();
        if (!int.TryParse(value, out number) || number <= 0)
        {
            error = $"{header} '{value}' must be a positive number.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static MatterForgeUser? FindUser(List<MatterForgeUser> users, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = NormalizeLookup(value);
        return users.FirstOrDefault(x =>
            NormalizeLookup(x.DisplayName) == normalizedValue ||
            NormalizeLookup(x.Email) == normalizedValue ||
            NormalizeLookup($"{x.FirstName} {x.LastName}") == normalizedValue);
    }

    private static string NormalizePartyRole(string value)
    {
        var normalized = NormalizeLookup(value);
        return normalized switch
        {
            "client" => PartyRoles.Client,
            "adverseparty" or "adverse" or "opposingparty" => PartyRoles.AdverseParty,
            "relatedparty" or "related" => PartyRoles.RelatedParty,
            "witness" => PartyRoles.Witness,
            "opposingcounsel" or "counsel" => PartyRoles.OpposingCounsel,
            "affiliate" or "subsidiary" or "affiliatesubsidiary" => PartyRoles.Affiliate,
            "other" => PartyRoles.Other,
            _ => PartyRoles.All.FirstOrDefault(x => NormalizeLookup(x) == normalized) ?? string.Empty
        };
    }

    private static string NormalizeLookup(string value)
    {
        return new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string DefaultIfBlank(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string AppendImportNote(string existingNotes, ImportBatch batch)
    {
        var note = $"Imported/updated from {batch.FileName} on {batch.CreatedAt.LocalDateTime:g}.";
        return string.IsNullOrWhiteSpace(existingNotes)
            ? note
            : existingNotes.Contains(note, StringComparison.OrdinalIgnoreCase)
                ? existingNotes
                : $"{existingNotes.Trim()}{Environment.NewLine}{note}";
    }

    private sealed class CsvTable
    {
        private readonly Dictionary<string, string> headerMap;

        private CsvTable(List<string> headers, List<CsvRow> rows)
        {
            Headers = headers;
            Rows = rows;
            headerMap = headers.ToDictionary(NormalizeLookup, x => x, StringComparer.OrdinalIgnoreCase);
        }

        public List<string> Headers { get; }

        public List<CsvRow> Rows { get; }

        public bool HasHeader(string header)
        {
            return headerMap.ContainsKey(NormalizeLookup(header));
        }

        public static async Task<CsvTable> ReadAsync(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var content = await reader.ReadToEndAsync();
            var records = Parse(content)
                .Where(x => x.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                .ToList();

            if (records.Count == 0)
            {
                return new CsvTable([], []);
            }

            var headers = records[0].Select(x => x.Trim()).ToList();
            var rows = records
                .Skip(1)
                .Select((record, index) => CsvRow.FromRecord(index + 2, headers, record))
                .ToList();

            return new CsvTable(headers, rows);
        }

        private static List<List<string>> Parse(string content)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < content.Length; i++)
            {
                var current = content[i];
                if (current == '"')
                {
                    if (inQuotes && i + 1 < content.Length && content[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (current == ',' && !inQuotes)
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    continue;
                }

                if ((current == '\r' || current == '\n') && !inQuotes)
                {
                    if (current == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                    {
                        i++;
                    }

                    row.Add(cell.ToString());
                    rows.Add(row);
                    row = [];
                    cell.Clear();
                    continue;
                }

                cell.Append(current);
            }

            row.Add(cell.ToString());
            rows.Add(row);
            return rows;
        }
    }

    private sealed class CsvRow
    {
        private readonly Dictionary<string, string> valuesByHeader;

        private CsvRow(int rowNumber, Dictionary<string, string> values)
        {
            RowNumber = rowNumber;
            Values = values;
            valuesByHeader = values.ToDictionary(x => NormalizeLookup(x.Key), x => x.Value, StringComparer.OrdinalIgnoreCase);
        }

        public int RowNumber { get; }

        public Dictionary<string, string> Values { get; }

        public string Value(string header)
        {
            return valuesByHeader.TryGetValue(NormalizeLookup(header), out var value) ? value : string.Empty;
        }

        public static CsvRow FromRecord(int rowNumber, List<string> headers, List<string> record)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                values[headers[i]] = i < record.Count ? record[i].Trim() : string.Empty;
            }

            return new CsvRow(rowNumber, values);
        }
    }
}
