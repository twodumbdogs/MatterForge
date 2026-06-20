namespace CMIForge.Services;

public class DemoResetHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<DemoResetHostedService> logger) : BackgroundService
{
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

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var resetService = scope.ServiceProvider.GetRequiredService<DemoResetService>();
            await resetService.ResetAsync("Scheduled reset", stoppingToken);
        }
    }
}
