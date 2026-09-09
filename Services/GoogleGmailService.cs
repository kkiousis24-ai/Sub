using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Sub.Api.Models;

namespace Sub.Api.Services;

public class GoogleGmailService
{
    private GmailService CreateService(ConnectedEmailAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.AccessToken))
        {
            throw new InvalidOperationException(
                "The Gmail account does not have a valid access token."
            );
        }

        var credential = GoogleCredential
            .FromAccessToken(account.AccessToken);

        return new GmailService(
            new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Sub"
            }
        );
    }

    public async Task<GmailProfileResponse> GetProfileAsync(
        ConnectedEmailAccount account)
    {
        using var gmailService = CreateService(account);

        var profile = await gmailService.Users
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

    public async Task<List<GmailMessageResponse>> GetSubscriptionEmailsAsync(
        ConnectedEmailAccount account)
    {
        using var gmailService = CreateService(account);

        var listRequest = gmailService.Users.Messages.List("me");

        // Πρώτο MVP φίλτρο για emails που πιθανόν αφορούν subscriptions
        listRequest.Q =
            "newer_than:2y {subscription renewal invoice receipt payment charged membership}";

        listRequest.MaxResults = 25;

        var listResponse = await listRequest.ExecuteAsync();

        var results = new List<GmailMessageResponse>();

        if (listResponse.Messages == null)
        {
            return results;
        }

        foreach (var item in listResponse.Messages)
        {
            var getRequest =
                gmailService.Users.Messages.Get("me", item.Id);

            getRequest.Format =
                UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;

            getRequest.MetadataHeaders =
                new[] { "From", "Subject", "Date" };

            Message message = await getRequest.ExecuteAsync();

            var headers = message.Payload?.Headers;

            string? from = headers?
                .FirstOrDefault(h => h.Name == "From")?
                .Value;

            string? subject = headers?
                .FirstOrDefault(h => h.Name == "Subject")?
                .Value;

            string? date = headers?
                .FirstOrDefault(h => h.Name == "Date")?
                .Value;

            results.Add(new GmailMessageResponse
            {
                MessageId = message.Id,
                ThreadId = message.ThreadId,
                From = from,
                Subject = subject,
                Date = date,
                Snippet = message.Snippet
            });
        }

        return results;
    }
}

public class GmailProfileResponse
{
    public string? EmailAddress { get; set; }

    public long? MessagesTotal { get; set; }

    public long? ThreadsTotal { get; set; }

    public ulong? HistoryId { get; set; }
}

public class GmailMessageResponse
{
    public string? MessageId { get; set; }

    public string? ThreadId { get; set; }

    public string? From { get; set; }

    public string? Subject { get; set; }

    public string? Date { get; set; }

    public string? Snippet { get; set; }
}