using CMIForge.Data;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public class TenantBrandingService(CMIForgeDbContext db, IConfiguration configuration)
{
    public const string FirmNameSettingKey = "Branding.FirmName";

    public async Task<string> GetDisplayNameAsync()
    {
        var settingName = await db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key == FirmNameSettingKey)
            .Select(x => x.Value)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(settingName))
        {
            return settingName.Trim();
        }

        var configuredName = configuration["CMIForge:BrandName"];
        return string.IsNullOrWhiteSpace(configuredName)
            ? ProductInfo.Name
            : configuredName.Trim();
    }
}
