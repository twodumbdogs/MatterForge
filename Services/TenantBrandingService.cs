using MatterForge.Data;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Services;

public class TenantBrandingService(MatterForgeDbContext db, IConfiguration configuration)
{
    public const string FirmNameSettingKey = "Branding.FirmName";

    public async Task<string> GetDisplayNameAsync()
    {
        var configuredName = configuration["MatterForge:BrandName"];
        if (!string.IsNullOrWhiteSpace(configuredName))
        {
            return configuredName.Trim();
        }

        var settingName = await db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key == FirmNameSettingKey)
            .Select(x => x.Value)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(settingName)
            ? ProductInfo.Name
            : settingName.Trim();
    }
}
