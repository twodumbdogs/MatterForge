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
    private const string DefaultCurrentUserEmail = "imauser@twodumbdogs.com";
    private const string DefaultCurrentUserDisplayName = "Ima User";

    public async Task<MatterForgeUser?> GetCurrentUserAsync()
    {
        var email = GetAuthenticatedEmail();
        if (!string.IsNullOrWhiteSpace(email))
        {
            return await GetOrCreateAuthenticatedUserAsync(email);
        }

        if (entraOptions.Value.Enabled && !configuration.GetValue<bool>("MatterForge:DemoMode"))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            var configuredEmail = configuration["MatterForge:CurrentUserEmail"];
            email = string.IsNullOrWhiteSpace(configuredEmail)
                ? DefaultCurrentUserEmail
                : configuredEmail.Trim();
        }

        var configuredUser = await db.Users
            .FirstOrDefaultAsync(x => x.IsActive && x.Email == email);
        if (configuredUser is not null)
        {
            return configuredUser;
        }

        var configuredDisplayName = configuration["MatterForge:CurrentUserDisplayName"];
        var displayName = string.IsNullOrWhiteSpace(configuredDisplayName)
            ? DefaultCurrentUserDisplayName
            : configuredDisplayName.Trim();

        return await db.Users
            .FirstOrDefaultAsync(x => x.IsActive && x.DisplayName == displayName)
            ?? await db.Users
                .Where(x => x.IsActive)
                .OrderBy(x => x.SystemId)
                .FirstOrDefaultAsync();
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
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return FirstClaimValue(
            user,
            ClaimTypes.Email,
            "preferred_username",
            "upn",
            "email");
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

    private async Task<MatterForgeUser?> GetOrCreateAuthenticatedUserAsync(string email)
    {
        var existingUser = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (existingUser is not null)
        {
            return existingUser.IsActive ? existingUser : null;
        }

        var userLimit = await productPlanService.GetUserLimitAsync();
        if (!userLimit.CanCreate)
        {
            return null;
        }

        var principal = httpContextAccessor.HttpContext?.User;
        var displayName =
            FirstClaimValue(principal!, "name", ClaimTypes.Name) ??
            email.Split('@', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ??
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
            Email = email,
            Title = string.Empty,
            IsActive = true
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
