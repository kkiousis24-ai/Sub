using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Sub.Api.Data;
using Sub.Api.Models;

namespace Sub.Api.Services;

public class GoogleGmailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;

    public GoogleGmailService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        AppDbContext context)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _context = context;
    }

    // =========================================================
    // Create authenticated Gmail service
    // =========================================================

    private async Task<GmailService> CreateServiceAsync(
        ConnectedEmailAccount account)
    {
        var accessToken =
            await GetValidAccessTokenAsync(account);

        var credential =
            GoogleCredential.FromAccessToken(accessToken);

        return new GmailService(
            new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Sub"
            });
    }

    // =========================================================
    // Get valid access token
    // =========================================================

    private async Task<string> GetValidAccessTokenAsync(
        ConnectedEmailAccount account)
    {
        var tokenStillValid =
            !string.IsNullOrWhiteSpace(account.AccessToken) &&
            account.TokenExpiresAt.HasValue &&
            account.TokenExpiresAt.Value >
            DateTime.UtcNow.AddMinutes(2);

        if (tokenStillValid)
        {
            return account.AccessToken!;
        }

        if (string.IsNullOrWhiteSpace(account.RefreshToken))
        {
            throw new InvalidOperationException(
                "The Gmail access token has expired and no refresh token is available. Please reconnect the Gmail account."
            );
        }

        return await RefreshAccessTokenAsync(account);
    }

    // =========================================================
    // Refresh Google access token
    // =========================================================

    private async Task<string> RefreshAccessTokenAsync(
        ConnectedEmailAccount account)
    {
        var clientId =
            _configuration["Authentication:Google:ClientId"];

        var clientSecret =
            _configuration["Authentication:Google:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException(
                "Google ClientId is missing.");
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "Google ClientSecret is missing.");
        }

        if (string.IsNullOrWhiteSpace(account.RefreshToken))
        {
            throw new InvalidOperationException(
                "Google refresh token is missing.");
        }

        var httpClient =
            _httpClientFactory.CreateClient();

        var requestData =
            new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "refresh_token", account.RefreshToken },
                { "grant_type", "refresh_token" }
            };

        using var requestContent =
            new FormUrlEncodedContent(requestData);

        var response =
            await httpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                requestContent);

        var responseContent =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Google token refresh failed. Status: {(int)response.StatusCode}. Response: {responseContent}"
            );
        }

        GoogleTokenRefreshResponse? tokenResponse;

        try
        {
            tokenResponse =
                JsonSerializer.Deserialize<GoogleTokenRefreshResponse>(
                    responseContent);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Google returned an invalid token refresh response.",
                ex);
        }

        if (tokenResponse == null ||
            string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException(
                "Google did not return a new access token.");
        }

        account.AccessToken =
            tokenResponse.AccessToken;

        var expiresInSeconds =
            tokenResponse.ExpiresIn > 0
                ? tokenResponse.ExpiresIn
                : 3600;

        account.TokenExpiresAt =
            DateTime.UtcNow.AddSeconds(expiresInSeconds);

        await _context.SaveChangesAsync();

        return account.AccessToken;
    }

    // =========================================================
    // Gmail Profile
    // =========================================================

    public async Task<GmailProfileResponse> GetProfileAsync(
        ConnectedEmailAccount account)
    {
        using var gmailService =
            await CreateServiceAsync(account);

        var profile =
            await gmailService.Users
                .GetProfile("me")
                .ExecuteAsync();

        return new GmailProfileResponse
        {
            EmailAddress = profile.EmailAddress,
            MessagesTotal = profile.MessagesTotal,
            ThreadsTotal = profile.ThreadsTotal,
            HistoryId = profile.HistoryId
        };
    }

    // =========================================================
    // Subscription-related emails
    // =========================================================

    public async Task<List<GmailMessageResponse>>
        GetSubscriptionEmailsAsync(
            ConnectedEmailAccount account)
    {
        using var gmailService =
            await CreateServiceAsync(account);

        var listRequest =
            gmailService.Users.Messages.List("me");

        listRequest.Q =
            "newer_than:2y {subscription renewal invoice receipt payment charged membership}";

        listRequest.MaxResults = 25;

        var listResponse =
            await listRequest.ExecuteAsync();

        var results =
            new List<GmailMessageResponse>();

        if (listResponse.Messages == null)
        {
            return results;
        }

        foreach (var item in listResponse.Messages)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                continue;
            }

            var getRequest =
                gmailService.Users.Messages.Get(
                    "me",
                    item.Id);

            // =================================================
            // IMPORTANT
            // Full instead of Metadata
            // ώστε να μπορούμε να διαβάσουμε το email body.
            // =================================================

            getRequest.Format =
                UsersResource.MessagesResource
                    .GetRequest
                    .FormatEnum
                    .Full;

            Message message =
                await getRequest.ExecuteAsync();

            var headers =
                message.Payload?.Headers;

            var from =
                GetHeader(headers, "From");

            var subject =
                GetHeader(headers, "Subject");

            var date =
                GetHeader(headers, "Date");

            // =================================================
            // Extract readable body
            // =================================================

            var bodyText =
                ExtractBodyText(message.Payload);

            results.Add(
                new GmailMessageResponse
                {
                    MessageId =
                        message.Id,

                    ThreadId =
                        message.ThreadId,

                    From =
                        from,

                    Subject =
                        subject,

                    Date =
                        date,

                    Snippet =
                        message.Snippet,

                    BodyText =
                        bodyText
                });
        }

        return results;
    }

    // =========================================================
    // Header helper
    // =========================================================

    private static string? GetHeader(
        IList<MessagePartHeader>? headers,
        string headerName)
    {
        return headers?
            .FirstOrDefault(
                h => string.Equals(
                    h.Name,
                    headerName,
                    StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    // =========================================================
    // Extract message body recursively
    //
    // Gmail emails can be:
    // text/plain
    // text/html
    // multipart/alternative
    // multipart/mixed
    // =========================================================

    private static string ExtractBodyText(
        MessagePart? payload)
    {
        if (payload == null)
        {
            return string.Empty;
        }

        var plainTextParts =
            new List<string>();

        var htmlParts =
            new List<string>();

        CollectBodyParts(
            payload,
            plainTextParts,
            htmlParts);

        // Prefer plain text.
        if (plainTextParts.Count > 0)
        {
            return string.Join(
                Environment.NewLine,
                plainTextParts);
        }

        // Fallback to HTML converted to readable text.
        if (htmlParts.Count > 0)
        {
            var html =
                string.Join(
                    Environment.NewLine,
                    htmlParts);

            return ConvertHtmlToText(html);
        }

        return string.Empty;
    }

    // =========================================================
    // Walk Gmail MIME structure
    // =========================================================

    private static void CollectBodyParts(
        MessagePart part,
        List<string> plainTextParts,
        List<string> htmlParts)
    {
        var mimeType =
            part.MimeType?.ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(
            part.Body?.Data))
        {
            var decoded =
                DecodeBase64Url(
                    part.Body.Data);

            if (!string.IsNullOrWhiteSpace(decoded))
            {
                if (mimeType == "text/plain")
                {
                    plainTextParts.Add(decoded);
                }
                else if (mimeType == "text/html")
                {
                    htmlParts.Add(decoded);
                }
            }
        }

        if (part.Parts == null)
        {
            return;
        }

        foreach (var childPart in part.Parts)
        {
            CollectBodyParts(
                childPart,
                plainTextParts,
                htmlParts);
        }
    }

    // =========================================================
    // Gmail uses Base64 URL encoding
    // =========================================================

    private static string DecodeBase64Url(
        string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return string.Empty;
        }

        try
        {
            var base64 =
                encoded
                    .Replace('-', '+')
                    .Replace('_', '/');

            switch (base64.Length % 4)
            {
                case 2:
                    base64 += "==";
                    break;

                case 3:
                    base64 += "=";
                    break;
            }

            var bytes =
                Convert.FromBase64String(base64);

            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    // =========================================================
    // Basic HTML -> readable text
    // Δεν αποθηκεύουμε HTML.
    // =========================================================

    private static string ConvertHtmlToText(
        string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text =
            System.Text.RegularExpressions.Regex.Replace(
                html,
                @"<script[\s\S]*?</script>",
                " ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        text =
            System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<style[\s\S]*?</style>",
                " ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        text =
            System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<[^>]+>",
                " ");

        text =
            System.Net.WebUtility.HtmlDecode(text);

        text =
            System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\s+",
                " ");

        return text.Trim();
    }

    // =========================================================
    // Google Token Refresh DTO
    // =========================================================

    private sealed class GoogleTokenRefreshResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }
}

// =============================================================
// Gmail Profile DTO
// =============================================================

public class GmailProfileResponse
{
    public string? EmailAddress { get; set; }

    public long? MessagesTotal { get; set; }

    public long? ThreadsTotal { get; set; }

    public ulong? HistoryId { get; set; }
}

// =============================================================
// Gmail Message DTO
// =============================================================

public class GmailMessageResponse
{
    public string? MessageId { get; set; }

    public string? ThreadId { get; set; }

    public string? From { get; set; }

    public string? Subject { get; set; }

    public string? Date { get; set; }

    public string? Snippet { get; set; }

    public string? BodyText { get; set; }
}