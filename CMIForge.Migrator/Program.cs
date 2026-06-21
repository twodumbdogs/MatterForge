using CMIForge.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings:DefaultConnection is required.");
    return 2;
}

var seedCoreData = configuration.GetValue("CMIForgeMigrator:SeedCoreData", false);
var seedSampleData = configuration.GetValue("CMIForgeMigrator:SeedSampleData", false);

var options = new DbContextOptionsBuilder<CMIForgeDbContext>()
    .UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(180);
    })
    .Options;

await using var db = new CMIForgeDbContext(options);

Console.WriteLine("CMIForge database migrator starting.");
Console.WriteLine($"Target database: {db.Database.GetDbConnection().Database}");

var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToList();
if (pendingMigrations.Count == 0)
{
    Console.WriteLine("No pending EF migrations.");
}
else
{
    Console.WriteLine("Pending EF migrations:");
    foreach (var migration in pendingMigrations)
    {
        Console.WriteLine($"- {migration}");
    }
}

await db.Database.MigrateAsync();
Console.WriteLine("EF migrations applied.");

if (seedCoreData || seedSampleData)
{
    Console.WriteLine(seedSampleData
        ? "Seeding core and sample application data."
        : "Seeding core application data.");
    await SeedData.EnsureApplicationSeedDataAsync(db, seedSampleData);
    Console.WriteLine(seedSampleData
        ? "Core and sample application data seeded."
        : "Core application data seeded.");
}

Console.WriteLine("CMIForge database migrator finished.");
return 0;
