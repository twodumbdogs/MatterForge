using CMIForge.Data;
using CMIForge.Models;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public class EmailOutboxDispatcher(CMIForgeDbContext db, IEmailSender emailSender, ILogger<EmailOutboxDispatcher> logger)
{
    private const int MaxAttempts = 5;

    public async Task<int> DispatchBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var messages = await db.EmailOutboxMessages
            .Where(x => EmailOutboxStatuses.Active.Contains(x.Status) &&
                (x.NextAttemptAt == null || x.NextAttemptAt <= now) &&
                x.AttemptCount < MaxAttempts)
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var sentCount = 0;
        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            message.Status = EmailOutboxStatuses.Sending;
            message.AttemptCount++;
            message.LastAttemptAt = now;
            message.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);

            EmailSendResult result;
            try
            {
                result = await emailSender.SendAsync(message, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Email outbox message {MessageId} failed during send.", message.Id);
                result = new EmailSendResult(false, ex.Message);
            }

            var finishedAt = DateTimeOffset.UtcNow;
            if (result.Success)
            {
                message.Status = EmailOutboxStatuses.Sent;
                message.SentAt = finishedAt;
                message.LastError = string.Empty;
                message.NextAttemptAt = null;
                sentCount++;
            }
            else
            {
                message.Status = message.AttemptCount >= MaxAttempts
                    ? EmailOutboxStatuses.Failed
                    : EmailOutboxStatuses.Pending;
                message.LastError = result.Message;
                message.NextAttemptAt = message.AttemptCount >= MaxAttempts
                    ? null
                    : finishedAt.AddMinutes(Math.Min(60, Math.Pow(2, message.AttemptCount)));
            }

            message.UpdatedAt = finishedAt;
            await db.SaveChangesAsync(cancellationToken);
        }

        return sentCount;
    }
}

public class EmailOutboxHostedService(IServiceScopeFactory scopeFactory, ILogger<EmailOutboxHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<EmailOutboxDispatcher>();
                await dispatcher.DispatchBatchAsync(10, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Email outbox dispatch loop failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
