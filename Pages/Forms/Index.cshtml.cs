using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Forms;

public class IndexModel(
    CMIForgeDbContext db,
    PermissionService permissionService,
    AuditLogService auditLogService) : PageModel
{
    public List<FormDefinition> Forms { get; private set; } = [];

    public Dictionary<Guid, int> SubmissionCounts { get; private set; } = [];

    public Dictionary<Guid, InviteCountSummary> InviteCounts { get; private set; } = [];

    public bool CanSubmitForms { get; private set; }

    public bool CanManageFormWorkflowDefinitions { get; private set; }

    public async Task OnGetAsync()
    {
        CanSubmitForms = await permissionService.HasAsync(PermissionKeys.FormsSubmit);
        CanManageFormWorkflowDefinitions = await permissionService.HasAsync(PermissionKeys.FormsWorkflowsAdmin);

        Forms = await db.FormDefinitions
            .Include(x => x.Versions)
                .ThenInclude(x => x.WorkflowDefinition)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        SubmissionCounts = await db.FormSubmissions
            .GroupBy(x => x.FormDefinitionId)
            .Select(x => new { FormDefinitionId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.FormDefinitionId, x => x.Count);

        var now = DateTimeOffset.UtcNow;
        InviteCounts = await db.ExternalFormInvites
            .GroupBy(x => x.FormDefinitionId)
            .Select(x => new
            {
                FormDefinitionId = x.Key,
                Open = x.Count(invite => invite.Status == ExternalFormInviteStatuses.Open && invite.ExpiresAt > now),
                Completed = x.Count(invite => invite.Status == ExternalFormInviteStatuses.Completed)
            })
            .ToDictionaryAsync(x => x.FormDefinitionId, x => new InviteCountSummary(x.Open, x.Completed));
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.FormsWorkflowsAdmin))
        {
            return Forbid();
        }

        var form = await db.FormDefinitions
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (form is null)
        {
            return NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var openInvites = await db.ExternalFormInvites
            .Where(x => x.FormDefinitionId == id &&
                x.Status == ExternalFormInviteStatuses.Open &&
                x.ExpiresAt > now)
            .ToListAsync();
        foreach (var invite in openInvites)
        {
            invite.Status = ExternalFormInviteStatuses.Revoked;
            invite.UpdatedAt = now;
        }

        form.IsActive = false;
        form.UpdatedAt = now;
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Form.Retired",
            "FormDefinition",
            form.Id,
            form.Key,
            $"Retired form {form.Name}.",
            new { RevokedOpenInviteCount = openInvites.Count });

        return RedirectToPage("./Index");
    }

    public async Task<IActionResult> OnPostCopyAsync(Guid id)
    {
        if (!await permissionService.HasAsync(PermissionKeys.FormsDesign))
        {
            return Forbid();
        }

        var source = await db.FormDefinitions
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

        if (source is null)
        {
            return NotFound();
        }

        var sourceVersion = source.Versions
            .Where(x => x.IsPublished)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefault()
            ?? source.Versions.OrderByDescending(x => x.VersionNumber).FirstOrDefault();

        if (sourceVersion is null)
        {
            return RedirectToPage("./Index");
        }

        var now = DateTimeOffset.UtcNow;
        var copy = new FormDefinition
        {
            Name = CopyName(source.Name),
            Key = await CopyKeyAsync(source.Key),
            Description = source.Description,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Versions =
            [
                new FormVersion
                {
                    VersionNumber = 1,
                    SchemaJson = sourceVersion.SchemaJson,
                    WorkflowDefinitionId = sourceVersion.WorkflowDefinitionId,
                    IsPublished = true,
                    CreatedAt = now,
                    PublishedAt = now
                }
            ]
        };

        db.FormDefinitions.Add(copy);
        await db.SaveChangesAsync();
        await auditLogService.LogAsync(
            "Form.Copied",
            "FormDefinition",
            copy.Id,
            copy.Key,
            $"Copied form {source.Name} to {copy.Name}.",
            new { SourceFormDefinitionId = source.Id, SourceVersionNumber = sourceVersion.VersionNumber });

        return RedirectToPage("./Edit", new { id = copy.Id });
    }

    private static string CopyName(string sourceName)
    {
        var name = $"Copy of {sourceName}".Trim();
        return name.Length <= 160 ? name : name[..160];
    }

    private async Task<string> CopyKeyAsync(string sourceKey)
    {
        var root = string.IsNullOrWhiteSpace(sourceKey) ? "form" : sourceKey.Trim();
        root = root.Length > 68 ? root[..68].TrimEnd('-') : root;
        var candidate = $"{root}-copy";
        var index = 2;

        while (await db.FormDefinitions.AnyAsync(x => x.Key == candidate))
        {
            var suffix = $"-copy-{index++}";
            var maxRootLength = 80 - suffix.Length;
            var trimmedRoot = root.Length > maxRootLength ? root[..maxRootLength].TrimEnd('-') : root;
            candidate = $"{trimmedRoot}{suffix}";
        }

        return candidate;
    }
}

public sealed record InviteCountSummary(int Open, int Completed);
