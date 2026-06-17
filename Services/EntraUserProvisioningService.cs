using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;

namespace MatterForge.Services;

public sealed record EntraCreateUserRequest(
    string FirstName,
    string MiddleName,
    string LastName,
    string DisplayName,
    string UserName,
    string? JobTitle);

public sealed record EntraCreateUserResult(
    string TenantId,
    string ObjectId,
    string UserPrincipalName,
    string TemporaryPassword);

public class EntraUserProvisioningService(
    HttpClient httpClient,
    IConfiguration configuration)
{
    private static readonly Regex UserNamePattern = new("^[A-Za-z0-9._-]{1,64}$", RegexOptions.Compiled);
    private static readonly char[] PasswordCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!#$%*-_+".ToCharArray();
    private readonly TokenCredential credential = new DefaultAzureCredential();

    public bool IsEnabled => configuration.GetValue<bool>("EntraProvisioning:Enabled");

    public string Domain => NormalizeDomain(configuration["EntraProvisioning:Domain"]);

    public string TenantId => configuration["Authentication:Microsoft:TenantId"]?.Trim() ?? string.Empty;

    public bool IsConfigured => IsEnabled && !string.IsNullOrWhiteSpace(Domain);

    public string BuildUserPrincipalName(string userName)
    {
        var normalizedUserName = NormalizeUserName(userName);
        return $"{normalizedUserName}@{Domain}";
    }

    public async Task<EntraCreateUserResult> CreateUserAsync(EntraCreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Entra user provisioning is not configured.");
        }

        var userPrincipalName = BuildUserPrincipalName(request.UserName);
        var temporaryPassword = GenerateTemporaryPassword();
        var payload = new
        {
            accountEnabled = true,
            displayName = request.DisplayName,
            givenName = request.FirstName,
            surname = request.LastName,
            mailNickname = BuildMailNickname(request.UserName),
            userPrincipalName,
            jobTitle = request.JobTitle,
            passwordProfile = new
            {
                forceChangePasswordNextSignIn = true,
                password = temporaryPassword
            }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "https://graph.microsoft.com/v1.0/users");
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(["https://graph.microsoft.com/.default"]),
            cancellationToken);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Entra user provisioning failed: {ExtractGraphError(responseText)}");
        }

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        var objectId = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        var createdUpn = root.TryGetProperty("userPrincipalName", out var upnElement) ? upnElement.GetString() : userPrincipalName;
        if (string.IsNullOrWhiteSpace(objectId))
        {
            throw new InvalidOperationException("Entra created a user but did not return an object ID.");
        }

        return new EntraCreateUserResult(TenantId, objectId, createdUpn ?? userPrincipalName, temporaryPassword);
    }

    public static bool IsValidUserName(string userName)
    {
        return UserNamePattern.IsMatch(userName.Trim());
    }

    private static string NormalizeDomain(string? value)
    {
        return value?.Trim().TrimStart('@').ToLowerInvariant() ?? string.Empty;
    }

    private static string NormalizeUserName(string value)
    {
        var normalized = value.Trim().Trim('@').ToLowerInvariant();
        if (!IsValidUserName(normalized))
        {
            throw new InvalidOperationException("Use letters, numbers, periods, underscores, or hyphens for the Entra login username.");
        }

        return normalized;
    }

    private static string BuildMailNickname(string userName)
    {
        var nickname = Regex.Replace(userName.Trim().ToLowerInvariant(), "[^a-z0-9._-]", string.Empty);
        nickname = nickname.Trim('.', '_', '-');
        if (string.IsNullOrWhiteSpace(nickname))
        {
            nickname = $"user{RandomNumberGenerator.GetInt32(100000, 999999)}";
        }

        return nickname.Length <= 64 ? nickname : nickname[..64];
    }

    private static string GenerateTemporaryPassword()
    {
        var password = new StringBuilder("Cmi1!");
        while (password.Length < 18)
        {
            password.Append(PasswordCharacters[RandomNumberGenerator.GetInt32(PasswordCharacters.Length)]);
        }

        return password.ToString();
    }

    private static string ExtractGraphError(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return "Microsoft Graph returned an empty error response.";
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? responseText;
            }
        }
        catch (JsonException)
        {
            // Fall through to the raw response text.
        }

        return responseText;
    }
}
