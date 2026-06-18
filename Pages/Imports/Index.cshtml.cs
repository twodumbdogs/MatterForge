using System.Text;
using MatterForge.Data;
using MatterForge.Models;
using MatterForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Pages.Imports;

public class IndexModel(
    MatterForgeDbContext db,
    CsvImportService csvImportService,
    CurrentUserService currentUserService,
    ProductPlanService productPlanService,
    PermissionService permissionService) : PageModel
{
    [BindProperty]
    public IFormFile? ClientFile { get; set; }

    [BindProperty]
    public IFormFile? MatterFile { get; set; }

    [BindProperty]
    public IFormFile? PartyFile { get; set; }

    [BindProperty]
    public OcrClientInput OcrInput { get; set; } = new();

    public List<ImportBatch> RecentBatches { get; private set; } = [];

    public ImportBatch? LatestBatch { get; private set; }

    public bool CanRunImports { get; private set; }

    [TempData]
    public Guid? LatestBatchId { get; set; }

    [TempData]
    public string? OcrImportMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.ImportsView))
        {
            return Forbid();
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnGetClientTemplateAsync()
    {
        return await DownloadTemplateAsync(
            "client-import-template.csv",
            """
            ClientNumber,ClientName,Status,PrimaryContact,Email,Phone
            00000001,Acme Sample Holdings LLC,Active,Mina Q. Caldera,mina.caldera@example.com,555-0101
            """);
    }

    public async Task<IActionResult> OnGetMatterTemplateAsync()
    {
        return await DownloadTemplateAsync(
            "matter-import-template.csv",
            """
            MatterNumber,MatterName,ClientNumber,Status,ResponsibleAttorney,PracticeArea
            00000001,Sample Supply Agreement Review,00000001,Open,Ima User,Corporate
            """);
    }

    public async Task<IActionResult> OnGetPartyTemplateAsync()
    {
        return await DownloadTemplateAsync(
            "party-import-template.csv",
            """
            PartyName,Role,MatterNumber
            Stark & Stone Holdings LLC,Adverse Party,00000001
            Stark Stone LLP,Opposing Counsel,00000001
            """);
    }

    public async Task<IActionResult> OnPostValidateClientsAsync()
    {
        return await RunImportAsync(ClientFile, ImportTypes.Clients, validateOnly: true);
    }

    public async Task<IActionResult> OnPostClientsAsync()
    {
        return await RunImportAsync(ClientFile, ImportTypes.Clients, validateOnly: false);
    }

    public async Task<IActionResult> OnPostValidateMattersAsync()
    {
        return await RunImportAsync(MatterFile, ImportTypes.Matters, validateOnly: true);
    }

    public async Task<IActionResult> OnPostMattersAsync()
    {
        return await RunImportAsync(MatterFile, ImportTypes.Matters, validateOnly: false);
    }

    public async Task<IActionResult> OnPostValidatePartiesAsync()
    {
        return await RunImportAsync(PartyFile, ImportTypes.Parties, validateOnly: true);
    }

    public async Task<IActionResult> OnPostPartiesAsync()
    {
        return await RunImportAsync(PartyFile, ImportTypes.Parties, validateOnly: false);
    }

    public async Task<IActionResult> OnPostOcrClientAsync()
    {
        if (!await permissionService.HasAsync(PermissionKeys.ImportsRun))
        {
            return Forbid();
        }

        var clientLimit = await productPlanService.GetClientLimitAsync();
        if (!clientLimit.CanCreate)
        {
            ModelState.AddModelError(string.Empty, clientLimit.Message);
        }

        ApplyServerSideOcrFallback();

        if (string.IsNullOrWhiteSpace(OcrInput.Name))
        {
            ModelState.AddModelError("OcrInput.Name", "Review the OCR result and enter a client name before creating the client.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var reviewedName = OcrInput.Name!.Trim();
        var nextNumber = (await db.Clients.MaxAsync(x => (int?)x.ClientNumber) ?? 0) + 1;
        var currentUser = await currentUserService.GetCurrentUserAsync();

        db.Clients.Add(new Client
        {
            Name = reviewedName,
            ClientNumber = nextNumber,
            Status = "Active",
            PrimaryContact = OcrInput.PrimaryContact?.Trim() ?? string.Empty,
            Email = OcrInput.Email?.Trim() ?? string.Empty,
            Phone = OcrInput.Phone?.Trim() ?? string.Empty,
            AddressLine1 = OcrInput.AddressLine1?.Trim() ?? string.Empty,
            AddressLine2 = OcrInput.AddressLine2?.Trim() ?? string.Empty,
            City = OcrInput.City?.Trim() ?? string.Empty,
            State = OcrInput.State?.Trim() ?? string.Empty,
            PostalCode = OcrInput.PostalCode?.Trim() ?? string.Empty,
            Country = OcrInput.Country?.Trim() ?? string.Empty,
            Notes = BuildOcrNote(currentUser?.DisplayName)
        });

        await db.SaveChangesAsync();
        OcrImportMessage = $"Created client {nextNumber:D8} from reviewed OCR draft.";
        return RedirectToPage("./Index");
    }

    private async Task<IActionResult> RunImportAsync(IFormFile? file, string importType, bool validateOnly)
    {
        if (!await permissionService.HasAsync(PermissionKeys.ImportsRun))
        {
            return Forbid();
        }

        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(string.Empty, $"Choose a CSV file before {(validateOnly ? "validating" : "importing")}.");
            await LoadAsync();
            return Page();
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Use a .csv file for imports.");
            await LoadAsync();
            return Page();
        }

        var currentUser = await currentUserService.GetCurrentUserAsync();
        await using var stream = file.OpenReadStream();
        var batch = importType switch
        {
            ImportTypes.Clients => validateOnly
                ? await csvImportService.ValidateClientsAsync(stream, file.FileName, currentUser?.Id)
                : await csvImportService.ImportClientsAsync(stream, file.FileName, currentUser?.Id),
            ImportTypes.Matters => validateOnly
                ? await csvImportService.ValidateMattersAsync(stream, file.FileName, currentUser?.Id)
                : await csvImportService.ImportMattersAsync(stream, file.FileName, currentUser?.Id),
            ImportTypes.Parties => validateOnly
                ? await csvImportService.ValidatePartiesAsync(stream, file.FileName, currentUser?.Id)
                : await csvImportService.ImportPartiesAsync(stream, file.FileName, currentUser?.Id),
            _ => throw new InvalidOperationException($"Unsupported import type '{importType}'.")
        };

        LatestBatchId = batch.Id;
        return RedirectToPage("./Index");
    }

    private async Task<IActionResult> DownloadTemplateAsync(string fileName, string csv)
    {
        if (!await permissionService.HasAsync(PermissionKeys.ImportsView))
        {
            return Forbid();
        }

        var bytes = Encoding.UTF8.GetBytes(csv.Trim() + Environment.NewLine);
        return File(bytes, "text/csv", fileName);
    }

    private async Task LoadAsync()
    {
        CanRunImports = await permissionService.HasAsync(PermissionKeys.ImportsRun);

        RecentBatches = await db.ImportBatches
            .Include(x => x.ImportedByUser)
            .Include(x => x.Rows)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync();

        if (LatestBatchId.HasValue)
        {
            LatestBatch = RecentBatches.FirstOrDefault(x => x.Id == LatestBatchId.Value)
                ?? await db.ImportBatches
                    .Include(x => x.ImportedByUser)
                .Include(x => x.Rows)
                    .FirstOrDefaultAsync(x => x.Id == LatestBatchId.Value);
        }
    }

    private void ApplyServerSideOcrFallback()
    {
        if (string.IsNullOrWhiteSpace(OcrInput.RawText))
        {
            return;
        }

        var draft = OcrClientDraftParser.Parse(OcrInput.RawText);
        OcrInput.Name = FillIfBlank(OcrInput.Name, draft.Name);
        OcrInput.PrimaryContact = FillIfBlank(OcrInput.PrimaryContact, draft.PrimaryContact);
        OcrInput.Email = FillIfBlank(OcrInput.Email, draft.Email);
        OcrInput.Phone = FillIfBlank(OcrInput.Phone, draft.Phone);
        OcrInput.AddressLine1 = FillIfBlank(OcrInput.AddressLine1, draft.AddressLine1);
        OcrInput.AddressLine2 = FillIfBlank(OcrInput.AddressLine2, draft.AddressLine2);
        OcrInput.City = FillIfBlank(OcrInput.City, draft.City);
        OcrInput.State = FillIfBlank(OcrInput.State, draft.State);
        OcrInput.PostalCode = FillIfBlank(OcrInput.PostalCode, draft.PostalCode);
        OcrInput.Country = FillIfBlank(OcrInput.Country, draft.Country);
    }

    private string BuildOcrNote(string? userName)
    {
        var rawText = (OcrInput.RawText ?? string.Empty).Trim();
        var clippedText = rawText.Length > 2000 ? rawText[..2000] + "..." : rawText;
        var importedBy = string.IsNullOrWhiteSpace(userName) ? "a user" : userName;

        return string.IsNullOrWhiteSpace(clippedText)
            ? $"Created from reviewed OCR draft by {importedBy}."
            : $"Created from reviewed OCR draft by {importedBy}.{Environment.NewLine}{Environment.NewLine}OCR text:{Environment.NewLine}{clippedText}";
    }

    private static string FillIfBlank(string? currentValue, string suggestedValue)
    {
        return string.IsNullOrWhiteSpace(currentValue) ? suggestedValue : currentValue.Trim();
    }
}

public class OcrClientInput
{
    public string? RawText { get; set; }

    public string? Name { get; set; }

    public string? PrimaryContact { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }
}
