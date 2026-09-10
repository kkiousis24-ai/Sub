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
    // Creates authenticated Gmail API client
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
            }
        );
    }

    // =========================================================
    // Returns valid Google Access Token
    //
    // If current token is still valid -> use it
    // If expired -> refresh automatically
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

        // =====================================================
        // Access token expired
        // Need refresh token
        // =====================================================

        if (string.IsNullOrWhiteSpace(account.RefreshToken))
        {
            throw new InvalidOperationException(
                "The Gmail access token has expired and no refresh token is available. Please reconnect the Gmail account."
            );
        }

        return await RefreshAccessTokenAsync(account);
    }

    // =========================================================
    // Refresh Google OAuth Access Token
    // =========================================================

    private async Task<string> RefreshAccessTokenAsync(
        ConnectedEmailAccount account)
    {
        var clientId =
            _configuration[
                "Authentication:Google:ClientId"
            ];

        var clientSecret =
            _configuration[
                "Authentication:Google:ClientSecret"
            ];

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException(
                "Google ClientId is missing."
            );
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "Google ClientSecret is missing."
            );
        }

        if (string.IsNullOrWhiteSpace(account.RefreshToken))
        {
            throw new InvalidOperationException(
                "Google refresh token is missing."
            );
        }

        var httpClient =
            _httpClientFactory.CreateClient();

        var requestData =
            new Dictionary<string, string>
            {
                {
                    "client_id",
                    clientId
                },
                {
                    "client_secret",
                    clientSecret
                },
                {
                    "refresh_token",
                    account.RefreshToken
                },
                {
                    "grant_type",
                    "refresh_token"
                }
            };

        using var requestContent =
            new FormUrlEncodedContent(requestData);

        var response =
            await httpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                requestContent
            );

        var responseContent =
            await response.Content.ReadAsStringAsync();

        // =====================================================
        // Google rejected refresh request
        // =====================================================

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
                JsonSerializer.Deserialize<
                    GoogleTokenRefreshResponse>(
                    responseContent
                );
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Google returned an invalid token refresh response.",
                ex
            );
        }

        if (
            tokenResponse == null ||
            string.IsNullOrWhiteSpace(
                tokenResponse.AccessToken)
        )
        {
            throw new InvalidOperationException(
                "Google did not return a new access token."
            );
        }

        // =====================================================
        // Update connected Gmail account
        // =====================================================

        account.AccessToken =
            tokenResponse.AccessToken;

        var expiresInSeconds =
            tokenResponse.ExpiresIn > 0
                ? tokenResponse.ExpiresIn
                : 3600;

        account.TokenExpiresAt =
            DateTime.UtcNow.AddSeconds(
                expiresInSeconds
            );

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
            EmailAddress =
                profile.EmailAddress,

            MessagesTotal =
                profile.MessagesTotal,

            ThreadsTotal =
                profile.ThreadsTotal,

            HistoryId =
                profile.HistoryId
        };
    }

    // =========================================================
    // Get subscription-related emails
    // =========================================================

    public async Task<List<GmailMessageResponse>>
        GetSubscriptionEmailsAsync(
            ConnectedEmailAccount account)
    {
        using var gmailService =
            await CreateServiceAsync(account);

        var listRequest =
            gmailService.Users.Messages.List("me");

        // =====================================================
        // MVP Gmail search filter
        // =====================================================

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

        // =====================================================
        // Retrieve message metadata
        // =====================================================

        foreach (var item in listResponse.Messages)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                continue;
            }

            var getRequest =
                gmailService.Users.Messages.Get(
                    "me",
                    item.Id
                );

            getRequest.Format =
                UsersResource.MessagesResource
                    .GetRequest
                    .FormatEnum
                    .Metadata;

            getRequest.MetadataHeaders =
                new[]
                {
                    "From",
                    "Subject",
                    "Date"
                };

            Message message =
                await getRequest.ExecuteAsync();

            var headers =
                message.Payload?.Headers;

            var from =
                headers?
                    .FirstOrDefault(
                        h =>
                            string.Equals(
                                h.Name,
                                "From",
                                StringComparison.OrdinalIgnoreCase
                            )
                    )
                    ?.Value;

            var subject =
                headers?
                    .FirstOrDefault(
                        h =>
                            string.Equals(
                                h.Name,
                                "Subject",
                                StringComparison.OrdinalIgnoreCase
                            )
                    )
                    ?.Value;

            var date =
                headers?
                    .FirstOrDefault(
                        h =>
                            string.Equals(
                                h.Name,
                                "Date",
                                StringComparison.OrdinalIgnoreCase
                            )
                    )
                    ?.Value;

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
                        message.Snippet
                }
            );
        }

        return results;
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
}