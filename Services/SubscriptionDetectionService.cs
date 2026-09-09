using System.Globalization;
using System.Text.RegularExpressions;
using Sub.Api.Models;

namespace Sub.Api.Services;

public class SubscriptionDetectionService
    : ISubscriptionDetectionService
{
    // Πολύ ισχυρές ενδείξεις recurring subscription
    private static readonly string[] StrongRecurringPhrases =
    {
        "subscription will be renewing",
        "subscription will automatically renew",
        "automatically renew",
        "automatic renewal",
        "subscription renewed",
        "subscription renewal",
        "renewing soon",
        "recurring payment",
        "recurring charge",
        "auto-renew",
        "auto renew"
    };

    // Επιβεβαίωση αγοράς / ενεργοποίησης subscription
    private static readonly string[] PurchasePhrases =
    {
        "subscription purchase confirmation",
        "thanks for subscribing",
        "thank you for subscribing",
        "subscription activated",
        "membership activated",
        "membership confirmation",
        "subscription confirmation"
    };

    // Cancellation emails
    private static readonly string[] CancellationPhrases =
    {
        "subscription cancellation confirmation",
        "canceled subscription confirmation",
        "cancelled subscription confirmation",
        "canceled subscription",
        "cancelled subscription",
        "subscription has been canceled",
        "subscription has been cancelled",
        "membership canceled",
        "membership cancelled"
    };

    private static readonly string[] GenericSubscriptionWords =
    {
        "subscription",
        "subscriber",
        "subscribing",
        "membership"
    };

    private static readonly string[] PaymentPhrases =
    {
        "payment confirmation",
        "payment received",
        "recent payment",
        "your payment",
        "payment successful",
        "charged",
        "has been charged",
        "billed",
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

    private static readonly string[] ManagementPhrases =
    {
        "manage subscription",
        "manage your subscription",
        "cancel subscription",
        "cancel your subscription",
        "subscription settings",
        "manage membership",
        "billing settings"
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
        "save up to"
    };

    public SubscriptionDetectionResult Detect(
        EmailCandidate email)
    {
        var result = new SubscriptionDetectionResult();

        var subject = email.Subject ?? "";
        var snippet = email.Snippet ?? "";
        var from = email.From ?? "";

        var text =
            $"{subject} {snippet}"
                .ToLowerInvariant();

        var fromLower =
            from.ToLowerInvariant();

        var score = 0;

        // ==================================================
        // 1. Strong recurring evidence
        // ==================================================

        var recurringPhrase =
            FindFirstMatch(
                text,
                StrongRecurringPhrases);

        if (recurringPhrase != null)
        {
            score += 5;

            result.Reasons.Add(
                $"Strong recurring signal: {recurringPhrase}"
            );
        }

        // ==================================================
        // 2. Cancellation
        // ==================================================

        var cancellationPhrase =
            FindFirstMatch(
                text,
                CancellationPhrases);

        if (cancellationPhrase != null)
        {
            score += 5;

            result.Reasons.Add(
                $"Subscription cancellation signal: {cancellationPhrase}"
            );
        }

        // ==================================================
        // 3. Subscription purchase
        //
        // Δίνουμε purchase score μόνο αν ΔΕΝ είναι
        // cancellation email.
        // Έτσι ένα:
        // "Subscription Cancellation Confirmation"
        // δεν μετράει και σαν purchase.
        // ==================================================

        var purchasePhrase =
            FindFirstMatch(
                text,
                PurchasePhrases);

        if (purchasePhrase != null &&
            cancellationPhrase == null)
        {
            score += 5;

            result.Reasons.Add(
                $"Subscription purchase signal: {purchasePhrase}"
            );
        }

        // ==================================================
        // 4. Generic subscription terminology
        // ==================================================

        var subscriptionWord =
            FindFirstMatch(
                text,
                GenericSubscriptionWords);

        if (subscriptionWord != null)
        {
            score += 2;

            result.Reasons.Add(
                $"Subscription terminology: {subscriptionWord}"
            );
        }

        // ==================================================
        // 5. Payment evidence
        // ==================================================

        var paymentPhrase =
            FindFirstMatch(
                text,
                PaymentPhrases);

        if (paymentPhrase != null)
        {
            score += 2;

            result.Reasons.Add(
                $"Payment signal: {paymentPhrase}"
            );
        }

        // ==================================================
        // 6. Billing frequency
        // ==================================================

        result.BillingPeriod =
            DetectBillingPeriod(text);

        if (result.BillingPeriod != null)
        {
            score += 2;

            result.Reasons.Add(
                $"Billing period detected: {result.BillingPeriod}"
            );
        }

        // ==================================================
        // 7. Manage / cancel wording
        // ==================================================

        var managementPhrase =
            FindFirstMatch(
                text,
                ManagementPhrases);

        if (managementPhrase != null)
        {
            score += 2;

            result.Reasons.Add(
                $"Subscription management signal: {managementPhrase}"
            );
        }

        // ==================================================
        // 8. Price / currency
        // ==================================================

        result.Amount =
            ExtractAmount(
                text,
                out var currency);

        if (result.Amount.HasValue)
        {
            score += 2;

            result.Currency = currency;

            result.Reasons.Add(
                $"Price detected: {result.Amount} {currency}"
            );
        }

        // ==================================================
        // 9. Sender signals
        // ==================================================

        if (
            fromLower.Contains("billing") ||
            fromLower.Contains("payment") ||
            fromLower.Contains("purchase")
        )
        {
            score += 1;

            result.Reasons.Add(
                "Sender appears related to billing or purchases"
            );
        }

        // ==================================================
        // 10. Marketing penalty
        // ==================================================

        var marketingPhrase =
            FindFirstMatch(
                text,
                MarketingPhrases);

        if (
            marketingPhrase != null &&
            recurringPhrase == null &&
            purchasePhrase == null &&
            cancellationPhrase == null
        )
        {
            score -= 3;

            result.Reasons.Add(
                $"Possible marketing email: {marketingPhrase}"
            );
        }

        // ==================================================
        // Merchant
        // ==================================================

        result.Merchant =
            ExtractMerchant(from);

        // Δεν επιστρέφουμε αρνητικό score
        result.Score =
            Math.Max(score, 0);

        // ==================================================
        // Final decision
        // ==================================================

        result.IsSubscription =
            result.Score >= 5;

        result.Confidence =
            result.Score switch
            {
                >= 9 => "High",
                >= 5 => "Medium",
                _ => "Low"
            };

        return result;
    }

    // ======================================================
    // Find first matching phrase
    // ======================================================

    private static string? FindFirstMatch(
        string text,
        IEnumerable<string> phrases)
    {
        return phrases.FirstOrDefault(
            phrase => text.Contains(phrase)
        );
    }

    // ======================================================
    // Billing period
    // ======================================================

    private static string? DetectBillingPeriod(
        string text)
    {
        if (
            MonthlyPhrases.Any(
                phrase => text.Contains(phrase)))
        {
            return "Monthly";
        }

        if (
            YearlyPhrases.Any(
                phrase => text.Contains(phrase)))
        {
            return "Yearly";
        }

        if (
            WeeklyPhrases.Any(
                phrase => text.Contains(phrase)))
        {
            return "Weekly";
        }

        return null;
    }

    // ======================================================
    // Price extraction
    // ======================================================

    private static decimal? ExtractAmount(
        string text,
        out string? currency)
    {
        currency = null;

        var patterns =
            new Dictionary<string, string>
            {
                {
                    "EUR",
                    @"(?:€\s?|eur\s?)(\d+(?:[.,]\d{1,2})?)"
                },

                {
                    "USD",
                    @"(?:\$\s?|usd\s?)(\d+(?:[.,]\d{1,2})?)"
                },

                {
                    "GBP",
                    @"(?:£\s?|gbp\s?)(\d+(?:[.,]\d{1,2})?)"
                }
            };

        foreach (var pattern in patterns)
        {
            var match =
                Regex.Match(
                    text,
                    pattern.Value,
                    RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                continue;
            }

            var amountString =
                match.Groups[1]
                    .Value
                    .Replace(',', '.');

            if (
                decimal.TryParse(
                    amountString,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var amount))
            {
                currency =
                    pattern.Key;

                return amount;
            }
        }

        return null;
    }

    // ======================================================
    // Merchant extraction
    // ======================================================

    private static string? ExtractMerchant(
        string from)
    {
        if (string.IsNullOrWhiteSpace(from))
        {
            return null;
        }

        // Example:
        // Netflix <info@account.netflix.com>
        // -> Netflix

        var nameMatch =
            Regex.Match(
                from,
                @"^(.*?)\s*<");

        if (nameMatch.Success)
        {
            var merchant =
                nameMatch
                    .Groups[1]
                    .Value
                    .Trim()
                    .Trim('"');

            if (!string.IsNullOrWhiteSpace(merchant))
            {
                return merchant;
            }
        }

        // Example:
        // purchase-noreply@twitch.tv
        // -> Twitch

        var emailMatch =
            Regex.Match(
                from,
                @"@([^.]+)");

        if (emailMatch.Success)
        {
            var domain =
                emailMatch
                    .Groups[1]
                    .Value;

            if (!string.IsNullOrWhiteSpace(domain))
            {
                return char.ToUpper(domain[0]) +
                       domain[1..];
            }
        }

        return from;
    }
}