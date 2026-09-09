using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;
using Sub.Api.Models;

namespace Sub.Api.Services;

public class SubscriptionDetectionService
{
    private readonly AppDbContext _context;

    public SubscriptionDetectionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Subscription>> DetectAndSaveAsync(
        ConnectedEmailAccount account,
        List<GmailMessageResponse> messages)
    {
        var detectedSubscriptions = new List<Subscription>();

        foreach (var message in messages)
        {
            var combinedText =
                $"{message.From} {message.Subject} {message.Snippet}"
                    .ToLowerInvariant();

            var merchant = DetectMerchant(combinedText);

            if (merchant == null)
            {
                continue;
            }

            var amountResult = DetectAmount(combinedText);

            if (amountResult == null)
            {
                continue;
            }

            var billingCycle = DetectBillingCycle(combinedText);

            var confidence = CalculateConfidence(
                combinedText,
                merchant,
                amountResult.Value.Amount
            );

            var alreadyExists =
                await _context.Subscriptions.AnyAsync(s =>
                    s.UserId == account.UserId &&
                    s.ConnectedEmailAccountId == account.Id &&
                    s.Merchant == merchant &&
                    s.Amount == amountResult.Value.Amount &&
                    s.Status == "Active"
                );

            if (alreadyExists)
            {
                continue;
            }

            var subscription = new Subscription
            {
                UserId = account.UserId,
                ConnectedEmailAccountId = account.Id,

                Merchant = merchant,

                PlanName = DetectPlanName(
                    merchant,
                    message.Subject
                ),

                Amount = amountResult.Value.Amount,

                Currency = amountResult.Value.Currency,

                BillingCycle = billingCycle,

                NextBillingDate = null,

                Status = "Active",

                ConfidenceScore = confidence,

                CancellationUrl =
                    GetCancellationUrl(merchant),

                CreatedAt = DateTime.UtcNow
            };

            _context.Subscriptions.Add(subscription);

            detectedSubscriptions.Add(subscription);
        }

        await _context.SaveChangesAsync();

        return detectedSubscriptions;
    }

    private string? DetectMerchant(string text)
    {
        var merchants = new Dictionary<string, string>
        {
            { "netflix", "Netflix" },
            { "spotify", "Spotify" },
            { "youtube premium", "YouTube Premium" },
            { "google one", "Google One" },
            { "icloud", "iCloud" },
            { "apple music", "Apple Music" },
            { "disney+", "Disney+" },
            { "disney plus", "Disney+" },
            { "amazon prime", "Amazon Prime" },
            { "prime video", "Amazon Prime" },
            { "adobe", "Adobe" },
            { "microsoft 365", "Microsoft 365" },
            { "office 365", "Microsoft 365" },
            { "dropbox", "Dropbox" },
            { "canva", "Canva" },
            { "chatgpt", "ChatGPT" },
            { "openai", "ChatGPT" },
            { "claude", "Claude" },
            { "anthropic", "Claude" }
        };

        foreach (var merchant in merchants)
        {
            if (text.Contains(merchant.Key))
            {
                return merchant.Value;
            }
        }

        return null;
    }

    private (decimal Amount, string Currency)? DetectAmount(
        string text)
    {
        var patterns = new[]
        {
            @"€\s*(\d+[.,]?\d*)",
            @"(\d+[.,]?\d*)\s*€",

            @"\$\s*(\d+[.,]?\d*)",
            @"(\d+[.,]?\d*)\s*(usd|dollars?)",

            @"£\s*(\d+[.,]?\d*)",
            @"(\d+[.,]?\d*)\s*(gbp|pounds?)",

            @"(\d+[.,]?\d*)\s*eur"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(
                text,
                pattern,
                RegexOptions.IgnoreCase
            );

            if (!match.Success)
            {
                continue;
            }

            var amountText = match.Groups[1]
                .Value
                .Replace(",", ".");

            if (!decimal.TryParse(
                    amountText,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var amount))
            {
                continue;
            }

            var currency = "EUR";

            if (pattern.Contains(@"\$") ||
                text.Contains("usd"))
            {
                currency = "USD";
            }

            if (pattern.Contains("£") ||
                text.Contains("gbp"))
            {
                currency = "GBP";
            }

            return (amount, currency);
        }

        return null;
    }

    private string DetectBillingCycle(string text)
    {
        if (
            text.Contains("yearly") ||
            text.Contains("annual") ||
            text.Contains("annually") ||
            text.Contains("per year") ||
            text.Contains("/year")
        )
        {
            return "Yearly";
        }

        if (
            text.Contains("weekly") ||
            text.Contains("per week") ||
            text.Contains("/week")
        )
        {
            return "Weekly";
        }

        return "Monthly";
    }

    private string? DetectPlanName(
        string merchant,
        string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        if (subject.Contains(
                merchant,
                StringComparison.OrdinalIgnoreCase))
        {
            return subject;
        }

        return null;
    }

    private double CalculateConfidence(
        string text,
        string merchant,
        decimal amount)
    {
        double confidence = 0.45;

        if (!string.IsNullOrWhiteSpace(merchant))
        {
            confidence += 0.20;
        }

        if (amount > 0)
        {
            confidence += 0.15;
        }

        var subscriptionKeywords = new[]
        {
            "subscription",
            "renewal",
            "renew",
            "membership",
            "charged",
            "payment",
            "invoice",
            "receipt",
            "billing"
        };

        foreach (var keyword in subscriptionKeywords)
        {
            if (text.Contains(keyword))
            {
                confidence += 0.04;
            }
        }

        return Math.Min(confidence, 0.99);
    }

    private string? GetCancellationUrl(string merchant)
    {
        return merchant switch
        {
            "Netflix" =>
                "https://www.netflix.com/cancelplan",

            "Spotify" =>
                "https://www.spotify.com/account/",

            "YouTube Premium" =>
                "https://www.youtube.com/paid_memberships",

            "Amazon Prime" =>
                "https://www.amazon.com/gp/subs/primeclub/account/homepage.html",

            "Adobe" =>
                "https://account.adobe.com/plans",

            "Microsoft 365" =>
                "https://account.microsoft.com/services/",

            "Dropbox" =>
                "https://www.dropbox.com/account/plan",

            "Canva" =>
                "https://www.canva.com/settings/billing-and-plans",

            "ChatGPT" =>
                "https://chatgpt.com/",

            "Claude" =>
                "https://claude.ai/",

            _ => null
        };
    }
}