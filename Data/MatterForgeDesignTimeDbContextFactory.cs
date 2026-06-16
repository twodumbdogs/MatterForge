using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MatterForge.Data;

public class MatterForgeDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MatterForgeDbContext>
{
    public MatterForgeDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<MatterForgeDesignTimeDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("YOUR_SERVER", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Set ConnectionStrings:DefaultConnection before running EF migrations.");
        }

        var options = new DbContextOptionsBuilder<MatterForgeDbContext>()
            .UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
                sqlOptions.CommandTimeout(60);
            })
            .Options;

        return new MatterForgeDbContext(options);
    }
}
