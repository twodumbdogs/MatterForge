using CMIForge.Data;
using CMIForge.Models;
using CMIForge.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Pages.Forms;

public class IndexModel(CMIForgeDbContext db, PermissionService permissionService) : PageModel
{
    public List<FormDefinition> Forms { get; private set; } = [];

    public Dictionary<Guid, int> SubmissionCounts { get; private set; } = [];

    public Dictionary<Guid, InviteCountSummary> InviteCounts { get; private set; } = [];

    public bool CanSubmitForms { get; private set; }

    public async Task OnGetAsync()
    {
        CanSubmitForms = await permissionService.HasAsync(PermissionKeys.FormsSubmit);

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
}

public sealed record InviteCountSummary(int Open, int Completed);
