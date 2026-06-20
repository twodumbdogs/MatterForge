using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using CMIForge.Models;
using Microsoft.Extensions.Options;

namespace CMIForge.Services;

public sealed record EmailSendResult(bool Success, string Message);

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailOutboxMessage message, CancellationToken cancellationToken);
}

public class GraphEmailSender(IHttpClientFactory httpClientFactory, IOptions<GraphMailOptions> options) : IEmailSender
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TokenRequestContext GraphTokenContext = new(["https://graph.microsoft.com/.default"]);

    public async Task<EmailSendResult> SendAsync(EmailOutboxMessage message, CancellationToken cancellationToken)
    {
        var graphOptions = options.Value;
        if (!graphOptions.Enabled)
        {
            return new EmailSendResult(false, "Microsoft Graph mail sender is disabled.");
        }

        if (string.IsNullOrWhiteSpace(message.MailboxAddress))
        {
            return new EmailSendResult(false, "Mailbox address is missing.");
        }

        if (string.IsNullOrWhiteSpace(message.FromEmail))
        {
            return new EmailSendResult(false, "From email is missing.");
        }

        var recipients = SplitRecipients(message.ToRecipients).ToList();
        if (recipients.Count == 0)
        {
            return new EmailSendResult(false, "No recipients were supplied.");
        }

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

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(message.MailboxAddress)}/sendMail");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Content = JsonContent.Create(new
        {
            message = new
            {
                subject = message.Subject,
                body = new
                {
                    contentType = message.IsBodyHtml ? "HTML" : "Text",
                    content = message.Body
                },
                from = Mailbox(message.FromEmail, message.FromName),
                replyTo = string.IsNullOrWhiteSpace(message.ReplyToEmail)
                    ? Array.Empty<object>()
                    : [Mailbox(message.ReplyToEmail, message.FromName)],
                toRecipients = recipients.Select(x => new
                {
                    emailAddress = new
                    {
                        address = x
                    }
                })
            },
            saveToSentItems = true
        }, options: SerializerOptions);

        var client = httpClientFactory.CreateClient(nameof(GraphEmailSender));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return new EmailSendResult(true, "Message accepted by Microsoft Graph.");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new EmailSendResult(false, $"Graph sendMail returned {(int)response.StatusCode} {response.ReasonPhrase}: {Trim(body, 1200)}");
    }

    private static object Mailbox(string address, string name)
    {
        return new
        {
            emailAddress = new
            {
                name = string.IsNullOrWhiteSpace(name) ? address : name,
                address
            }
        };
    }

    private static IEnumerable<string> SplitRecipients(string value)
    {
        return value
            .Split([';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Contains('@', StringComparison.Ordinal));
    }

    private static string Trim(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
