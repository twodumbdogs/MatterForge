using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using CMIForge.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CMIForge.Services;

public class GraphInboundEmailReader(
    CMIForgeDbContext db,
    IHttpClientFactory httpClientFactory,
    IOptions<GraphMailOptions> options,
    InboundEmailIntakeService intakeService,
    ILogger<GraphInboundEmailReader> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TokenRequestContext GraphTokenContext = new(["https://graph.microsoft.com/.default"]);

    public async Task<int> PollAsync(CancellationToken cancellationToken)
    {
        var graphOptions = options.Value;
        if (!graphOptions.Enabled)
        {
            return 0;
        }

        var settings = await db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key.StartsWith("InboundEmail."))
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase, cancellationToken);
        if (!InboundEmailSettings.Bool(settings, InboundEmailSettingKeys.Enabled, false))
        {
            return 0;
        }

        var mailboxAddress = InboundEmailSettings.Value(settings, InboundEmailSettingKeys.MailboxAddress);
        if (string.IsNullOrWhiteSpace(mailboxAddress))
        {
            return 0;
        }

        var client = httpClientFactory.CreateClient(nameof(GraphInboundEmailReader));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(graphOptions, cancellationToken));

        var batchSize = 10;
        var messages = await GetUnreadMessagesAsync(client, mailboxAddress, batchSize, cancellationToken);
        var processedCount = 0;
        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await db.InboundEmailMessages.AnyAsync(x => x.GraphMessageId == message.Id, cancellationToken))
                {
                    continue;
                }

                var bodyText = await GetBodyTextAsync(client, mailboxAddress, message.Id, cancellationToken);
                var attachments = message.HasAttachments
                    ? await GetAttachmentsAsync(client, mailboxAddress, message.Id, cancellationToken)
                    : [];
                var envelope = new InboundEmailEnvelope(
                    mailboxAddress,
                    message.Id,
                    message.InternetMessageId ?? string.Empty,
                    message.ConversationId ?? string.Empty,
                    message.From?.EmailAddress?.Address ?? string.Empty,
                    message.From?.EmailAddress?.Name ?? string.Empty,
                    message.Subject ?? string.Empty,
                    message.BodyPreview ?? string.Empty,
                    bodyText,
                    message.ReceivedDateTime ?? DateTimeOffset.UtcNow,
                    attachments);

                var result = await intakeService.ProcessAsync(envelope, cancellationToken);
                if (result.Success)
                {
                    processedCount++;
                    if (InboundEmailSettings.Bool(settings, InboundEmailSettingKeys.MarkProcessedAsRead, true))
                    {
                        await MarkReadAsync(client, mailboxAddress, message.Id, cancellationToken);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Inbound Graph message {GraphMessageId} failed during polling.", message.Id);
            }
        }

        return processedCount;
    }

    private static async Task<List<GraphMessage>> GetUnreadMessagesAsync(HttpClient client, string mailboxAddress, int batchSize, CancellationToken cancellationToken)
    {
        var path = $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(mailboxAddress)}/mailFolders/inbox/messages";
        var query = "?$filter=isRead eq false&$orderby=receivedDateTime asc" +
            "&$select=id,internetMessageId,conversationId,subject,bodyPreview,receivedDateTime,from,hasAttachments,isRead" +
            $"&$top={batchSize}";
        var response = await client.GetFromJsonAsync<GraphCollection<GraphMessage>>(path + query, SerializerOptions, cancellationToken);
        return response?.Value ?? [];
    }

    private static async Task<string> GetBodyTextAsync(HttpClient client, string mailboxAddress, string messageId, CancellationToken cancellationToken)
    {
        var path = $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(mailboxAddress)}/messages/{Uri.EscapeDataString(messageId)}?$select=body";
        var message = await client.GetFromJsonAsync<GraphMessageBodyResult>(path, SerializerOptions, cancellationToken);
        var content = message?.Body?.Content ?? string.Empty;
        return message?.Body?.ContentType?.Equals("html", StringComparison.OrdinalIgnoreCase) == true
            ? StripHtml(content)
            : content;
    }

    private static async Task<List<InboundEmailFile>> GetAttachmentsAsync(HttpClient client, string mailboxAddress, string messageId, CancellationToken cancellationToken)
    {
        var path = $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(mailboxAddress)}/messages/{Uri.EscapeDataString(messageId)}/attachments";
        var response = await client.GetFromJsonAsync<GraphCollection<GraphAttachment>>(path, SerializerOptions, cancellationToken);
        List<InboundEmailFile> files = [];
        foreach (var attachment in response?.Value ?? [])
        {
            if (!attachment.ODataType.EndsWith("fileAttachment", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(attachment.ContentBytes))
            {
                continue;
            }

            byte[] content;
            try
            {
                content = Convert.FromBase64String(attachment.ContentBytes);
            }
            catch (FormatException)
            {
                continue;
            }

            files.Add(new InboundEmailFile(
                attachment.Id,
                attachment.Name ?? "email-attachment",
                attachment.ContentType ?? "application/octet-stream",
                content));
        }

        return files;
    }

    private static async Task MarkReadAsync(HttpClient client, string mailboxAddress, string messageId, CancellationToken cancellationToken)
    {
        var path = $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(mailboxAddress)}/messages/{Uri.EscapeDataString(messageId)}";
        using var response = await client.PatchAsJsonAsync(path, new { isRead = true }, SerializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> GetTokenAsync(GraphMailOptions graphOptions, CancellationToken cancellationToken)
    {
        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(graphOptions.TenantId))
        {
            credentialOptions.TenantId = graphOptions.TenantId;
        }

        if (!string.IsNullOrWhiteSpace(graphOptions.ManagedIdentityClientId))
        {
            credentialOptions.ManagedIdentityClientId = graphOptions.ManagedIdentityClientId;
        }

        var token = await new DefaultAzureCredential(credentialOptions)
            .GetTokenAsync(GraphTokenContext, cancellationToken);
        return token.Token;
    }

    private static string StripHtml(string value)
    {
        return Regex.Replace(value, "<.*?>", " ").Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase).Trim();
    }
}

public class InboundEmailHostedService(IServiceScopeFactory scopeFactory, ILogger<InboundEmailHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var reader = scope.ServiceProvider.GetRequiredService<GraphInboundEmailReader>();
                await reader.PollAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Inbound email polling loop failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}

public sealed class GraphCollection<T>
{
    public List<T> Value { get; set; } = [];
}

public sealed class GraphMessage
{
    public string Id { get; set; } = string.Empty;

    public string? InternetMessageId { get; set; }

    public string? ConversationId { get; set; }

    public string? Subject { get; set; }

    public string? BodyPreview { get; set; }

    public DateTimeOffset? ReceivedDateTime { get; set; }

    public GraphRecipient? From { get; set; }

    public bool HasAttachments { get; set; }
}

public sealed class GraphMessageBodyResult
{
    public GraphItemBody? Body { get; set; }
}

public sealed class GraphItemBody
{
    public string? ContentType { get; set; }

    public string? Content { get; set; }
}

public sealed class GraphRecipient
{
    public GraphEmailAddress? EmailAddress { get; set; }
}

public sealed class GraphEmailAddress
{
    public string? Name { get; set; }

    public string? Address { get; set; }
}

public sealed class GraphAttachment
{
    [JsonPropertyName("@odata.type")]
    public string ODataType { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? ContentType { get; set; }

    public string? ContentBytes { get; set; }
}
