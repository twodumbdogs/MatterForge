using CMIForge.Data;
using CMIForge.Models;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public class DemoResetHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<DemoResetHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan MaximumSchedulerSleep = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var startupScope = scopeFactory.CreateScope();
        var demoMode = startupScope.ServiceProvider.GetRequiredService<DemoModeService>();
        if (!demoMode.IsEnabled || !demoMode.ScheduledResetEnabled)
        {
            return;
        }

        var interval = TimeSpan.FromHours(demoMode.ResetIntervalHours);
        logger.LogInformation("Demo reset scheduler is active with interval {Interval}.", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CMIForgeDbContext>();
                var resetService = scope.ServiceProvider.GetRequiredService<DemoResetService>();
                var lastCompletedAt = await db.DemoResetRuns
                    .Where(x => x.Status == DemoResetRunStatuses.Succeeded && x.CompletedAt.HasValue)
                    .OrderByDescending(x => x.CompletedAt)
                    .Select(x => x.CompletedAt)
                    .FirstOrDefaultAsync(stoppingToken);

                var now = DateTimeOffset.UtcNow;
                var nextDueAt = lastCompletedAt?.Add(interval) ?? now;
                if (nextDueAt <= now)
                {
                    await resetService.ResetAsync("Scheduled reset", stoppingToken);
                    await Task.Delay(RetryDelay, stoppingToken);
                    continue;
                }

                var delay = nextDueAt - now;
                if (delay > MaximumSchedulerSleep)
                {
                    delay = MaximumSchedulerSleep;
                }

                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Demo reset scheduler failed while checking reset due time.");
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }
}
