using MatterForge.Data;
using MatterForge.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace MatterForge.Services;

public class CurrentUserService(
    MatterForgeDbContext db,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor,
    IOptions<EntraAuthenticationOptions> entraOptions,
    ProductPlanService productPlanService)
{
    private const string AdministratorRoleKey = "administrator";
    private const string DefaultCurrentUserEmail = "imauser@twodumbdogs.com";
    private const string DefaultCurrentUserDisplayName = "Ima User";
    private static readonly TimeSpan LastLoginUpdateInterval = TimeSpan.FromMinutes(5);

    public async Task<MatterForgeUser?> GetCurrentUserAsync()
    {
        var authenticatedIdentity = GetAuthenticatedIdentity();
        if (authenticatedIdentity is not null)
        {
            return await GetOrCreateAuthenticatedUserAsync(authenticatedIdentity);
        }

        if (entraOptions.Value.Enabled && !configuration.GetValue<bool>("MatterForge:DemoMode"))
        {
            return null;
        }

        var configuredEmail = configuration["MatterForge:CurrentUserEmail"];
        var email = string.IsNullOrWhiteSpace(configuredEmail)
            ? DefaultCurrentUserEmail
            : configuredEmail.Trim();

        var configuredUser = await db.Users
            .FirstOrDefaultAsync(x => x.IsActive && x.Email == email);
        if (configuredUser is not null)
        {
            await StampLastLoginAsync(configuredUser);
            return configuredUser;
        }

        var configuredDisplayName = configuration["MatterForge:CurrentUserDisplayName"];
        var displayName = string.IsNullOrWhiteSpace(configuredDisplayName)
            ? DefaultCurrentUserDisplayName
            : configuredDisplayName.Trim();

        var fallbackUser = await db.Users
            .FirstOrDefaultAsync(x => x.IsActive && x.DisplayName == displayName)
            ?? await db.Users
                .Where(x => x.IsActive)
                .OrderBy(x => x.SystemId)
                .FirstOrDefaultAsync();
        if (fallbackUser is not null)
        {
            await StampLastLoginAsync(fallbackUser);
        }

        return fallbackUser;
    }

    public async Task<List<Guid>> GetCurrentUserTeamIdsAsync()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser is null)
        {
            return [];
        }

        return await db.TeamMembers
            .Where(x => x.UserId == currentUser.Id && x.Team != null && x.Team.IsActive)
            .Select(x => x.TeamId)
            .ToListAsync();
    }

    public bool IsEntraLoginEnabled()
    {
        return entraOptions.Value.Enabled && productPlanService.AllowsFeature(ProductFeatureKeys.EntraSso);
    }

    public string? GetAuthenticatedEmail()
    {
        return GetAuthenticatedIdentity()?.Email;
    }

    private AuthenticatedIdentity? GetAuthenticatedIdentity()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var email = FirstClaimValue(
            user,
            ClaimTypes.Email,
            "email");
        var userPrincipalName = FirstClaimValue(
            user,
            "preferred_username",
            "upn",
            ClaimTypes.Upn);
        var objectId = FirstClaimValue(
            user,
            "oid",
            "http://schemas.microsoft.com/identity/claims/objectidentifier",
            ClaimTypes.NameIdentifier);
        var tenantId = FirstClaimValue(
            user,
            "tid",
            "http://schemas.microsoft.com/identity/claims/tenantid");
        var displayName = FirstClaimValue(user, "name", ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(email) &&
            string.IsNullOrWhiteSpace(userPrincipalName) &&
            string.IsNullOrWhiteSpace(objectId))
        {
            return null;
        }

        return new AuthenticatedIdentity(
            email,
            userPrincipalName,
            objectId,
            tenantId,
            displayName);
    }

    private static string? FirstClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private async Task<MatterForgeUser?> GetOrCreateAuthenticatedUserAsync(AuthenticatedIdentity identity)
    {
        var email = identity.Email?.Trim();
        var userPrincipalName = identity.UserPrincipalName?.Trim();
        var isBootstrapAdmin = IsBootstrapAdminIdentity(identity);
        var existingUser = await FindExistingAuthenticatedUserAsync(identity);
        if (existingUser is not null)
        {
            if (!existingUser.IsActive && !isBootstrapAdmin)
            {
                return null;
            }

            if (isBootstrapAdmin)
            {
                existingUser.IsActive = true;
                await EnsureBootstrapAdminRoleAsync(existingUser);
            }

            ApplyEntraIdentity(existingUser, identity);
            existingUser.LastLoginAt = DateTimeOffset.UtcNow;
            existingUser.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return existingUser;
        }

        var userLimit = await productPlanService.GetUserLimitAsync();
        if (!userLimit.CanCreate && !isBootstrapAdmin)
        {
            return null;
        }

        var displayName =
            identity.DisplayName ??
            userPrincipalName?.Split('@', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ??
            email?.Split('@', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ??
            email;
        var nameParts = UserNameParts.FromDisplayName(displayName, email);

        var nextSystemId = (await db.Users.MaxAsync(x => (int?)x.SystemId) ?? 0) + 1;
        var user = new MatterForgeUser
        {
            SystemId = nextSystemId,
            FirstName = nameParts.FirstName,
            MiddleName = nameParts.MiddleName,
            LastName = nameParts.LastName,
            DisplayName = nameParts.DisplayName,
            Email = email ?? userPrincipalName ?? string.Empty,
            Title = string.Empty,
            IsActive = true,
            LastLoginAt = DateTimeOffset.UtcNow
        };
        ApplyEntraIdentity(user, identity);

        db.Users.Add(user);
        if (isBootstrapAdmin)
        {
            await EnsureBootstrapAdminRoleAsync(user);
        }

        await db.SaveChangesAsync();
        return user;
    }

    private async Task StampLastLoginAsync(MatterForgeUser user)
    {
        var now = DateTimeOffset.UtcNow;
        if (user.LastLoginAt.HasValue && now - user.LastLoginAt.Value < LastLoginUpdateInterval)
        {
            return;
        }

        await db.Users
            .Where(x => x.Id == user.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.LastLoginAt, now));
        user.LastLoginAt = now;
    }

    private async Task<MatterForgeUser?> FindExistingAuthenticatedUserAsync(AuthenticatedIdentity identity)
    {
        if (!string.IsNullOrWhiteSpace(identity.TenantId) && !string.IsNullOrWhiteSpace(identity.ObjectId))
        {
            var byObjectId = await db.Users.FirstOrDefaultAsync(x =>
                x.EntraTenantId == identity.TenantId &&
                x.EntraObjectId == identity.ObjectId);
            if (byObjectId is not null)
            {
                return byObjectId;
            }
        }

        if (!string.IsNullOrWhiteSpace(identity.UserPrincipalName))
        {
            var byUpn = await db.Users.FirstOrDefaultAsync(x => x.EntraUserPrincipalName == identity.UserPrincipalName);
            if (byUpn is not null)
            {
                return byUpn;
            }
        }

        if (!string.IsNullOrWhiteSpace(identity.Email))
        {
            var byEmail = await db.Users.FirstOrDefaultAsync(x => x.Email == identity.Email);
            if (byEmail is not null)
            {
                return byEmail;
            }
        }

        return !string.IsNullOrWhiteSpace(identity.UserPrincipalName)
            ? await db.Users.FirstOrDefaultAsync(x => x.Email == identity.UserPrincipalName)
            : null;
    }

    private bool IsBootstrapAdminIdentity(AuthenticatedIdentity identity)
    {
        var configuredEmails = configuration["MatterForge:BootstrapAdminEmail"];
        if (string.IsNullOrWhiteSpace(configuredEmails))
        {
            return false;
        }

        return configuredEmails
            .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(x =>
                x.Equals(identity.Email, StringComparison.OrdinalIgnoreCase) ||
                x.Equals(identity.UserPrincipalName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureBootstrapAdminRoleAsync(MatterForgeUser user)
    {
        var administratorRole = await db.SecurityRoles
            .FirstOrDefaultAsync(x => x.Key == AdministratorRoleKey && x.IsActive);
        if (administratorRole is null)
        {
            return;
        }

        var alreadyAssigned = await db.UserRoles
            .AnyAsync(x => x.UserId == user.Id && x.SecurityRoleId == administratorRole.Id);
        if (!alreadyAssigned)
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                SecurityRoleId = administratorRole.Id
            });
        }

        if (string.IsNullOrWhiteSpace(user.Title))
        {
            user.Title = "Administrator";
        }
    }

    private static void ApplyEntraIdentity(MatterForgeUser user, AuthenticatedIdentity identity)
    {
        if (!string.IsNullOrWhiteSpace(identity.TenantId))
        {
            user.EntraTenantId = identity.TenantId;
        }

        if (!string.IsNullOrWhiteSpace(identity.ObjectId))
        {
            user.EntraObjectId = identity.ObjectId;
        }

        if (!string.IsNullOrWhiteSpace(identity.UserPrincipalName))
        {
            user.EntraUserPrincipalName = identity.UserPrincipalName;
        }
    }

    private sealed record AuthenticatedIdentity(
        string? Email,
        string? UserPrincipalName,
        string? ObjectId,
        string? TenantId,
        string? DisplayName);
}
