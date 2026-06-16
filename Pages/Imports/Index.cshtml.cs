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
    PermissionService permissionService) : PageModel
{
    [BindProperty]
    public IFormFile? ClientFile { get; set; }

    [BindProperty]
    public IFormFile? MatterFile { get; set; }

    [BindProperty]
    public IFormFile? PartyFile { get; set; }

    public List<ImportBatch> RecentBatches { get; private set; } = [];

    public ImportBatch? LatestBatch { get; private set; }

    public bool CanRunImports { get; private set; }

    [TempData]
    public Guid? LatestBatchId { get; set; }

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
}
