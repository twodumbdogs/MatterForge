using System.Text.Json;
using CMIForge.Data;
using CMIForge.Models;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public class AuditLogService(
    CMIForgeDbContext db,
    CurrentUserService currentUserService,
    IHttpContextAccessor httpContextAccessor)
{
    public async Task LogAsync(
        string action,
        string entityType,
        object? entityId = null,
        string? entityNumber = null,
        string? summary = null,
        object? details = null)
    {
        var actor = await currentUserService.GetCurrentUserAsync();
        var httpContext = httpContextAccessor.HttpContext;
        var auditLog = new AuditLog
        {
            ActorUserId = actor?.Id,
            ActorDisplayName = actor?.DisplayName ?? "Unknown",
            ActorEmail = actor?.Email ?? string.Empty,
            Action = TrimToMax(action, 120),
            EntityType = TrimToMax(entityType, 80),
            EntityId = TrimToMax(Convert.ToString(entityId) ?? string.Empty, 80),
            EntityNumber = TrimToMax(entityNumber ?? string.Empty, 40),
            Summary = TrimToMax(summary ?? string.Empty, 500),
            DetailsJson = details is null ? "{}" : JsonSerializer.Serialize(details, FormJson.Options),
            IpAddress = TrimToMax(httpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty, 80),
            UserAgent = TrimToMax(httpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty, 500)
        };

        db.AuditLogs.Add(auditLog);

        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            db.Entry(auditLog).State = EntityState.Detached;
        }
    }

    public async Task<List<AuditLog>> ListForEntityAsync(string entityType, object entityId, int take = 10)
    {
        var id = Convert.ToString(entityId) ?? string.Empty;
        return await db.AuditLogs
            .AsNoTracking()
            .Include(x => x.ActorUser)
            .Where(x => x.EntityType == entityType && x.EntityId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    private static string TrimToMax(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
