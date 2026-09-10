using System.Globalization;
using System.Text.RegularExpressions;
using Sub.Api.Models;

namespace Sub.Api.Services;

public class SubscriptionDetectionService
    : ISubscriptionDetectionService
{
    private static readonly string[] StrongRecurringPhrases =
    {
        "subscription will be renewing",
        "subscription will automatically renew",
        "automatically renew",
        "automatic renewal",
        "subscription renewed",
        "subscription renewal",
        "renewing soon",
        "renews on",
        "will renew",
        "next renewal",
        "recurring payment",
        "recurring charge",
        "auto-renew",
        "auto renew"
    };

    private static readonly string[] ActivationPhrases =
    {
        "subscription is confirmed",
        "subscription confirmed",
        "subscription confirmation",
        "subscription activated",
        "subscription has been activated",
        "thanks for subscribing",
        "thank you for subscribing",
        "thanks for starting your",
        "thank you for starting your",
        "membership activated",
        "membership confirmation",
        "welcome to your subscription"
    };

    private static readonly string[] CancellationPhrases =
    {
        "subscription was canceled",
        "subscription was cancelled",
        "subscription has been canceled",
        "subscription has been cancelled",
        "subscription successfully canceled",
        "subscription successfully cancelled",
        "subscription cancellation",
        "subscription cancellation confirmation",
        "canceled subscription",
        "cancelled subscription",
        "canceled your subscription",
        "cancelled your subscription",
        "membership canceled",
        "membership cancelled"
    };

    private static readonly string[] TrialPhrases =
    {
        "free trial",
        "trial started",
        "trial has started",
        "trial ends",
        "trial expires",
        "trial will end",
        "trial will expire",
        "premium trial"
    };

    private static readonly string[] SubscriptionWords =
    {
        "subscription",
        "subscriber",
        "subscribing",
        "membership"
    };

    private static readonly string[] FutureBillingPhrases =
    {
        "next charge",
        "next payment",
        "next billing",
        "will be charged",
        "you will be charged",
        "billing date",
        "renews on",
        "renewal date"
    };

    private static readonly string[] PaymentPhrases =
    {
        "payment method has been charged",
        "has been charged",
        "payment successful",
        "payment received",
        "billing"
    };

    private static readonly string[] MonthlyPhrases =
    {
        "monthly",
        "every month",
        "per month",
        "/month",
        "each month"
    };

    private static readonly string[] YearlyPhrases =
    {
        "yearly",
        "annual",
        "annually",
        "every year",
        "per year",
        "/year"
    };

    private static readonly string[] WeeklyPhrases =
    {
        "weekly",
        "every week",
        "per week",
        "/week"
    };

    private static readonly string[] MarketingPhrases =
    {
        "newsletter",
        "special offer",
        "limited offer",
        "promo code",
        "shop now",
        "black friday",
        "sale ends",
        "save up to",
        "50% off",
        "25% off",
        "gift stocks",
        "invite friends"
    };

    private static readonly string[] OneOffTransactionPhrases =
    {
        "thank you for your purchase",
        "transaction was successful",
        "store transaction",
        "thanks for riding",
        "booking fee"
    };

    private static readonly string[] RefundPhrases =
    {
        "your refund",
        "refund processed",
        "processed the refund",
        "refund from",
        "refunded"
    };

    public SubscriptionDetectionResult Detect(
        EmailCandidate email)
    {
        var result = new SubscriptionDetectionResult();

        var subject = email.Subject ?? "";
        var snippet = email.Snippet ?? "";
        var from = email.From ?? "";

        var text =
            $"{subject} {snippet}".ToLowerInvariant();

        var fromLower =
            from.ToLowerInvariant();

        var score = 0;

        // Cancellation
        var cancellation =
            FindFirstMatch(text, CancellationPhrases);

        if (cancellation != null)
        {
            score += 8;

            result.SubscriptionStatus = "Canceled";

            result.Reasons.Add(
                $"Cancellation signal: {cancellation}");
        }

        // Activation
        var activation =
            FindFirstMatch(text, ActivationPhrases);

        if (activation != null &&
            cancellation == null)
        {
            score += 5;

            result.SubscriptionStatus = "Active";

            result.Reasons.Add(
                $"Activation signal: {activation}");
        }

        // Recurring
        var recurring =
            FindFirstMatch(text, StrongRecurringPhrases);

        if (recurring != null)
        {
            score += 5;

            if (result.SubscriptionStatus == "Unknown")
            {
                result.SubscriptionStatus = "Active";
            }

            result.Reasons.Add(
                $"Recurring signal: {recurring}");
        }

        // Future billing
        var futureBilling =
            FindFirstMatch(text, FutureBillingPhrases);

        if (futureBilling != null)
        {
            score += 4;

            if (result.SubscriptionStatus == "Unknown")
            {
                result.SubscriptionStatus = "Active";
            }

            result.Reasons.Add(
                $"Future billing signal: {futureBilling}");
        }

        // Trial
        var trial =
            FindFirstMatch(text, TrialPhrases);

        if (trial != null)
        {
            score += 3;

            if (result.SubscriptionStatus == "Unknown")
            {
                result.SubscriptionStatus = "Active";
            }

            result.Reasons.Add(
                $"Trial signal: {trial}");
        }

        if (trial != null &&
            fromLower.Contains("premium"))
        {
            score += 2;

            result.Reasons.Add(
                "Premium service sender");
        }

        // Generic subscription terminology
        var subscriptionWord =
            FindFirstMatch(text, SubscriptionWords);

        if (subscriptionWord != null)
        {
            score += 2;

            result.Reasons.Add(
                $"Subscription terminology: {subscriptionWord}");
        }

        // Payment
        var payment =
            FindFirstMatch(text, PaymentPhrases);

        if (payment != null)
        {
            score += 1;

            result.Reasons.Add(
                $"Payment signal: {payment}");
        }

        // Billing period
        result.BillingPeriod =
            DetectBillingPeriod(text);

        if (result.BillingPeriod != null)
        {
            score += 2;

            result.Reasons.Add(
                $"Billing period: {result.BillingPeriod}");
        }

        // Amount
        result.Amount =
            ExtractAmount(text, out var currency);

        if (result.Amount.HasValue)
        {
            score += 1;

            result.Currency = currency;

            result.Reasons.Add(
                $"Price detected: {result.Amount} {currency}");
        }

        // Refund penalty
        var refund =
            FindFirstMatch(text, RefundPhrases);

        if (refund != null &&
            cancellation == null)
        {
            score -= 6;

            result.Reasons.Add(
                $"Refund signal: {refund}");
        }

        // One-off purchase penalty
        var oneOff =
            FindFirstMatch(
                text,
                OneOffTransactionPhrases);

        if (oneOff != null &&
            activation == null &&
            recurring == null &&
            cancellation == null &&
            futureBilling == null)
        {
            score -= 5;

            result.Reasons.Add(
                $"Possible one-off transaction: {oneOff}");
        }

        // Marketing penalty
        var marketing =
            FindFirstMatch(text, MarketingPhrases);

        if (marketing != null &&
            activation == null &&
            recurring == null &&
            cancellation == null &&
            futureBilling == null &&
            trial == null)
        {
            score -= 4;

            result.Reasons.Add(
                $"Marketing signal: {marketing}");
        }

        result.Merchant =
            ExtractMerchant(from);

        result.Score =
            Math.Max(score, 0);

        result.IsSubscription =
            result.Score >= 5;

        result.Confidence =
            result.Score switch
            {
                >= 10 => "High",
                >= 5 => "Medium",
                _ => "Low"
            };

        return result;
    }

    private static string? FindFirstMatch(
        string text,
        IEnumerable<string> phrases)
    {
        return phrases.FirstOrDefault(
            phrase =>
                text.Contains(
                    phrase,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string? DetectBillingPeriod(
        string text)
    {
        if (MonthlyPhrases.Any(
            phrase => text.Contains(phrase)))
        {
            return "Monthly";
        }

        if (YearlyPhrases.Any(
            phrase => text.Contains(phrase)))
        {
            return "Yearly";
        }

        if (WeeklyPhrases.Any(
            phrase => text.Contains(phrase)))
        {
            return "Weekly";
        }

        return null;
    }

    private static decimal? ExtractAmount(
        string text,
        out string? currency)
    {
        currency = null;

        var patterns =
            new Dictionary<string, string[]>
            {
                {
                    "EUR",
                    new[]
                    {
                        @"(?:€\s*|eur\s*)(\d+(?:[.,]\d{1,2})?)",
                        @"(\d+(?:[.,]\d{1,2})?)\s*(?:€|eur)"
                    }
                },
                {
                    "USD",
                    new[]
                    {
                        @"(?:\$\s*|usd\s*)(\d+(?:[.,]\d{1,2})?)",
                        @"(\d+(?:[.,]\d{1,2})?)\s*usd"
                    }
                },
                {
                    "GBP",
                    new[]
                    {
                        @"(?:£\s*|gbp\s*)(\d+(?:[.,]\d{1,2})?)",
                        @"(\d+(?:[.,]\d{1,2})?)\s*(?:£|gbp)"
                    }
                }
            };

        foreach (var currencyPatterns in patterns)
        {
            foreach (var pattern in currencyPatterns.Value)
            {
                var match =
                    Regex.Match(
                        text,
                        pattern,
                        RegexOptions.IgnoreCase);

                if (!match.Success)
                {
                    continue;
                }

                var amountString =
                    match.Groups[1]
                        .Value
                        .Replace(',', '.');

                if (decimal.TryParse(
                    amountString,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var amount))
                {
                    currency =
                        currencyPatterns.Key;

                    return amount;
                }
            }
        }

        return null;
    }

    private static string? ExtractMerchant(
        string from)
    {
        if (string.IsNullOrWhiteSpace(from))
        {
            return null;
        }

        var nameMatch =
            Regex.Match(
                from,
                @"^(.*?)\s*<");

        if (nameMatch.Success)
        {
            var merchant =
                nameMatch.Groups[1]
                    .Value
                    .Trim()
                    .Trim('"');

            if (!string.IsNullOrWhiteSpace(merchant))
            {
                return merchant;
            }
        }

        var emailMatch =
            Regex.Match(
                from,
                @"@(?:[^.]+\.)*([^.@]+)\.[^.>]+");

        if (emailMatch.Success)
        {
            var domain =
                emailMatch.Groups[1]
                    .Value
                    .Trim();

            if (!string.IsNullOrWhiteSpace(domain))
            {
                return char.ToUpperInvariant(domain[0]) +
                       domain[1..];
            }
        }

        return from;
    }
}