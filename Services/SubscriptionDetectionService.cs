using System.Globalization;
using System.Text.RegularExpressions;
using Sub.Api.Models;

namespace Sub.Api.Services;

public class SubscriptionDetectionService
    : ISubscriptionDetectionService
{
    // =========================================================
    // Strong subscription / recurring signals
    // =========================================================

    private static readonly string[] StrongRecurringPhrases =
    {
        "subscription will be renewing",
        "subscription will automatically renew",
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

    // =========================================================
    // Activation
    // =========================================================

    private static readonly string[] ActivationPhrases =
    {
        "subscription is confirmed",
        "subscription confirmed",
        "subscription confirmation",
        "subscription activated",
        "subscription has been activated",
        "thanks for subscribing",
        "thank you for subscribing",
        "membership activated",
        "membership confirmation",
        "welcome to your subscription"
    };

    // =========================================================
    // Cancellation
    // =========================================================

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

    // =========================================================
    // Strong trial signals
    //
    // These imply that a trial actually exists.
    // "Free trial" alone is NOT enough.
    // =========================================================

    private static readonly string[] StrongTrialPhrases =
    {
        "trial started",
        "trial has started",
        "your trial has started",
        "trial ends",
        "trial expires",
        "trial will end",
        "trial will expire"
    };

    // =========================================================
    // Weak trial terminology
    // =========================================================

    private static readonly string[] WeakTrialPhrases =
    {
        "free trial",
        "premium trial"
    };

    // =========================================================
    // Generic subscription words
    // =========================================================

    private static readonly string[] SubscriptionWords =
    {
        "subscription",
        "subscriber",
        "subscribing",
        "membership"
    };

    // =========================================================
    // Future billing
    // =========================================================

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

    // =========================================================
    // Payment
    // =========================================================

    private static readonly string[] PaymentPhrases =
    {
        "payment method has been charged",
        "has been charged",
        "payment successful",
        "payment received",
        "amount paid",
        "total paid",
        "billing"
    };

    // =========================================================
    // Billing periods
    // =========================================================

    private static readonly string[] MonthlyPhrases =
    {
        "monthly",
        "every month",
        "per month",
        "/month",
        "/mo",
        "billed monthly",
        "charged monthly",
        "monthly subscription"
    };

    private static readonly string[] YearlyPhrases =
    {
        "yearly",
        "annual",
        "annually",
        "every year",
        "per year",
        "/year",
        "/yr",
        "billed yearly",
        "billed annually",
        "annual subscription"
    };

    private static readonly string[] WeeklyPhrases =
    {
        "weekly",
        "every week",
        "per week",
        "/week",
        "billed weekly"
    };

    // =========================================================
    // Marketing
    // =========================================================

    private static readonly string[] MarketingPhrases =
    {
        "newsletter",
        "special offer",
        "limited offer",
        "limited time",
        "promo code",
        "shop now",
        "black friday",
        "sale ends",
        "save up to",
        "last chance",
        "offer ends",
        "deal ends",
        "discount",
        "50% off",
        "40% off",
        "25% off",
        "gift stocks",
        "invite friends"
    };

    // =========================================================
    // One-off transactions
    // =========================================================

    private static readonly string[] OneOffTransactionPhrases =
    {
        "thank you for your purchase",
        "transaction was successful",
        "store transaction",
        "thanks for riding",
        "booking fee"
    };

    // =========================================================
    // Refunds
    // =========================================================

    private static readonly string[] RefundPhrases =
    {
        "your refund",
        "refund processed",
        "processed the refund",
        "refund from",
        "refunded"
    };

    // =========================================================
    // Detection
    // =========================================================

    public SubscriptionDetectionResult Detect(
        EmailCandidate email)
    {
        var result =
            new SubscriptionDetectionResult();

        var subject =
            email.Subject ?? string.Empty;

        var snippet =
            email.Snippet ?? string.Empty;

        var body =
            email.BodyText ?? string.Empty;

        var from =
            email.From ?? string.Empty;

        // =====================================================
        // IMPORTANT
        //
        // Subject + snippet = PRIMARY evidence
        // Full body         = SECONDARY evidence
        //
        // We no longer treat all three with the same weight.
        // =====================================================

        var primaryRawText =
            $"{subject} {snippet}";

        var primaryText =
            primaryRawText.ToLowerInvariant();

        var bodyText =
            body.ToLowerInvariant();

        var score = 0;

        // =====================================================
        // Cancellation
        // =====================================================

        var primaryCancellation =
            FindFirstMatch(
                primaryText,
                CancellationPhrases);

        var bodyCancellation =
            FindFirstMatch(
                bodyText,
                CancellationPhrases);

        var cancellation =
            primaryCancellation ??
            bodyCancellation;

        if (primaryCancellation != null)
        {
            score += 8;

            result.SubscriptionStatus =
                "Canceled";

            result.Reasons.Add(
                $"Cancellation signal (subject/snippet): " +
                $"{primaryCancellation}");
        }
        else if (bodyCancellation != null)
        {
            score += 5;

            result.SubscriptionStatus =
                "Canceled";

            result.Reasons.Add(
                $"Cancellation signal (body): " +
                $"{bodyCancellation}");
        }

        // =====================================================
        // Activation
        // =====================================================

        var primaryActivation =
            FindFirstMatch(
                primaryText,
                ActivationPhrases);

        var bodyActivation =
            FindFirstMatch(
                bodyText,
                ActivationPhrases);

        var activation =
            primaryActivation ??
            bodyActivation;

        if (cancellation == null)
        {
            if (primaryActivation != null)
            {
                score += 6;

                result.SubscriptionStatus =
                    "Active";

                result.Reasons.Add(
                    $"Activation signal (subject/snippet): " +
                    $"{primaryActivation}");
            }
            else if (bodyActivation != null)
            {
                score += 4;

                result.SubscriptionStatus =
                    "Active";

                result.Reasons.Add(
                    $"Activation signal (body): " +
                    $"{bodyActivation}");
            }
        }

        // =====================================================
        // Recurring payment
        // =====================================================

        var primaryRecurring =
            FindFirstMatch(
                primaryText,
                StrongRecurringPhrases);

        var bodyRecurring =
            FindFirstMatch(
                bodyText,
                StrongRecurringPhrases);

        var recurring =
            primaryRecurring ??
            bodyRecurring;

        if (primaryRecurring != null)
        {
            score += 6;

            if (result.SubscriptionStatus == "Unknown")
            {
                result.SubscriptionStatus =
                    "Active";
            }

            result.Reasons.Add(
                $"Recurring signal (subject/snippet): " +
                $"{primaryRecurring}");
        }
        else if (bodyRecurring != null)
        {
            score += 3;

            if (result.SubscriptionStatus == "Unknown")
            {
                result.SubscriptionStatus =
                    "Active";
            }

            result.Reasons.Add(
                $"Recurring signal (body): " +
                $"{bodyRecurring}");
        }

        // =====================================================
        // Future billing
        // =====================================================

        var primaryFutureBilling =
            FindFirstMatch(
                primaryText,
                FutureBillingPhrases);

        var bodyFutureBilling =
            FindFirstMatch(
                bodyText,
                FutureBillingPhrases);

        if (primaryFutureBilling != null)
        {
            score += 5;

            if (result.SubscriptionStatus == "Unknown")
            {
                result.SubscriptionStatus =
                    "Active";
            }

            result.Reasons.Add(
                $"Future billing signal (subject/snippet): " +
                $"{primaryFutureBilling}");
        }
        else if (bodyFutureBilling != null)
        {
            score += 2;

            result.Reasons.Add(
                $"Future billing signal (body): " +
                $"{bodyFutureBilling}");
        }

        // =====================================================
        // Next Billing Date
        //
        // Prefer subject/snippet first.
        // Only then search full body.
        // =====================================================

        result.NextBillingDate =
            ExtractNextBillingDate(
                primaryRawText);

        if (!result.NextBillingDate.HasValue)
        {
            result.NextBillingDate =
                ExtractNextBillingDate(body);
        }

        if (result.NextBillingDate.HasValue)
        {
            result.Reasons.Add(
                $"Next billing date detected: " +
                $"{result.NextBillingDate.Value:yyyy-MM-dd}");
        }

        // =====================================================
        // Strong trial
        // =====================================================

        var primaryStrongTrial =
            FindFirstMatch(
                primaryText,
                StrongTrialPhrases);

        var bodyStrongTrial =
            FindFirstMatch(
                bodyText,
                StrongTrialPhrases);

        if (primaryStrongTrial != null)
        {
            score += 4;

            if (result.SubscriptionStatus == "Unknown")
            {
                result.SubscriptionStatus =
                    "Active";
            }

            result.Reasons.Add(
                $"Trial signal (subject/snippet): " +
                $"{primaryStrongTrial}");
        }
        else if (bodyStrongTrial != null)
        {
            score += 2;

            if (result.SubscriptionStatus == "Unknown")
            {
                result.SubscriptionStatus =
                    "Active";
            }

            result.Reasons.Add(
                $"Trial signal (body): " +
                $"{bodyStrongTrial}");
        }

        // =====================================================
        // Weak trial terminology
        //
        // "Free trial" alone should not make an email
        // a subscription.
        // =====================================================

        var primaryWeakTrial =
            FindFirstMatch(
                primaryText,
                WeakTrialPhrases);

        var bodyWeakTrial =
            FindFirstMatch(
                bodyText,
                WeakTrialPhrases);

        if (primaryWeakTrial != null &&
            primaryStrongTrial == null)
        {
            score += 1;

            result.Reasons.Add(
                $"Trial terminology: " +
                $"{primaryWeakTrial}");
        }
        else if (bodyWeakTrial != null &&
                 bodyStrongTrial == null)
        {
            result.Reasons.Add(
                $"Trial terminology in body: " +
                $"{bodyWeakTrial}");
        }

        // =====================================================
        // Subscription terminology
        // =====================================================

        var primarySubscriptionWord =
            FindFirstMatch(
                primaryText,
                SubscriptionWords);

        var bodySubscriptionWord =
            FindFirstMatch(
                bodyText,
                SubscriptionWords);

        if (primarySubscriptionWord != null)
        {
            score += 2;

            result.Reasons.Add(
                $"Subscription terminology " +
                $"(subject/snippet): " +
                $"{primarySubscriptionWord}");
        }
        else if (bodySubscriptionWord != null)
        {
            score += 1;

            result.Reasons.Add(
                $"Subscription terminology (body): " +
                $"{bodySubscriptionWord}");
        }

        // =====================================================
        // Payment
        // =====================================================

        var primaryPayment =
            FindFirstMatch(
                primaryText,
                PaymentPhrases);

        var bodyPayment =
            FindFirstMatch(
                bodyText,
                PaymentPhrases);

        if (primaryPayment != null)
        {
            score += 2;

            result.Reasons.Add(
                $"Payment signal (subject/snippet): " +
                $"{primaryPayment}");
        }
        else if (bodyPayment != null)
        {
            score += 1;

            result.Reasons.Add(
                $"Payment signal (body): " +
                $"{bodyPayment}");
        }

        // =====================================================
        // Billing cycle
        // =====================================================

        var primaryBillingPeriod =
            DetectBillingPeriod(
                primaryText);

        var bodyBillingPeriod =
            DetectBillingPeriod(
                bodyText);

        if (primaryBillingPeriod != null)
        {
            result.BillingPeriod =
                primaryBillingPeriod;

            score += 2;

            result.Reasons.Add(
                $"Billing period (subject/snippet): " +
                $"{primaryBillingPeriod}");
        }
        else if (bodyBillingPeriod != null)
        {
            result.BillingPeriod =
                bodyBillingPeriod;

            score += 1;

            result.Reasons.Add(
                $"Billing period (body): " +
                $"{bodyBillingPeriod}");
        }

        // =====================================================
        // Amount + Currency
        //
        // Prefer subject/snippet.
        // Fall back to body.
        // =====================================================

        result.Amount =
            ExtractAmount(
                primaryText,
                out var currency);

        var amountFromPrimary =
            result.Amount.HasValue;

        if (!result.Amount.HasValue)
        {
            result.Amount =
                ExtractAmount(
                    bodyText,
                    out currency);
        }

        if (result.Amount.HasValue)
        {
            result.Currency =
                currency;

            score +=
                amountFromPrimary
                    ? 2
                    : 1;

            result.Reasons.Add(
                amountFromPrimary
                    ? $"Price detected (subject/snippet): " +
                      $"{result.Amount.Value} {currency}"
                    : $"Price detected (body): " +
                      $"{result.Amount.Value} {currency}");
        }

        // =====================================================
        // Refund penalty
        // =====================================================

        var primaryRefund =
            FindFirstMatch(
                primaryText,
                RefundPhrases);

        var bodyRefund =
            FindFirstMatch(
                bodyText,
                RefundPhrases);

        if (cancellation == null)
        {
            if (primaryRefund != null)
            {
                score -= 7;

                result.Reasons.Add(
                    $"Refund signal (subject/snippet): " +
                    $"{primaryRefund}");
            }
            else if (bodyRefund != null)
            {
                score -= 4;

                result.Reasons.Add(
                    $"Refund signal (body): " +
                    $"{bodyRefund}");
            }
        }

        // =====================================================
        // One-off transaction penalty
        // =====================================================

        var primaryOneOff =
            FindFirstMatch(
                primaryText,
                OneOffTransactionPhrases);

        var bodyOneOff =
            FindFirstMatch(
                bodyText,
                OneOffTransactionPhrases);

        if (activation == null &&
            recurring == null &&
            cancellation == null &&
            primaryFutureBilling == null &&
            bodyFutureBilling == null)
        {
            if (primaryOneOff != null)
            {
                score -= 6;

                result.Reasons.Add(
                    $"Possible one-off transaction " +
                    $"(subject/snippet): " +
                    $"{primaryOneOff}");
            }
            else if (bodyOneOff != null)
            {
                score -= 3;

                result.Reasons.Add(
                    $"Possible one-off transaction (body): " +
                    $"{bodyOneOff}");
            }
        }

        // =====================================================
        // Marketing detection
        //
        // Marketing in the SUBJECT is especially important.
        // Example:
        // "Last chance: save 40%"
        //
        // Promotional terms in a footer must not be enough
        // to create a subscription.
        // =====================================================

        var subjectMarketing =
            FindMarketingSignal(
                subject.ToLowerInvariant());

        var primaryMarketing =
            FindMarketingSignal(
                primaryText);

        var bodyMarketing =
            FindMarketingSignal(
                bodyText);

        // =====================================================
        // Strong evidence flags
        // =====================================================

        var hasPrimaryStrongSignal =
            primaryCancellation != null ||
            primaryActivation != null ||
            primaryRecurring != null ||
            primaryFutureBilling != null ||
            primaryStrongTrial != null;

        var hasStrongBodySignal =
            bodyCancellation != null ||
            bodyActivation != null ||
            bodyRecurring != null ||
            bodyStrongTrial != null;

        // =====================================================
        // Marketing penalty
        // =====================================================

        if (subjectMarketing != null &&
            !hasPrimaryStrongSignal)
        {
            score -= 10;

            result.Reasons.Add(
                $"Strong marketing subject: " +
                $"{subjectMarketing}");
        }
        else if (primaryMarketing != null &&
                 !hasPrimaryStrongSignal)
        {
            score -= 7;

            result.Reasons.Add(
                $"Marketing signal (subject/snippet): " +
                $"{primaryMarketing}");
        }
        else if (bodyMarketing != null &&
                 !hasPrimaryStrongSignal &&
                 !hasStrongBodySignal)
        {
            score -= 3;

            result.Reasons.Add(
                $"Marketing signal (body): " +
                $"{bodyMarketing}");
        }

        // =====================================================
        // Merchant
        // =====================================================

        result.Merchant =
            ExtractMerchant(from);

        // =====================================================
        // Final score
        // =====================================================

        result.Score =
            Math.Max(
                score,
                0);

        // =====================================================
        // Final validation
        //
        // A high score is NOT enough by itself.
        //
        // We require at least one real subscription event:
        // - activation
        // - recurring renewal
        // - cancellation
        // - confirmed trial
        // - strong future billing in subject/snippet
        //
        // This blocks promotional emails whose body only
        // contains generic pricing / subscription language.
        // =====================================================

        var hasRealSubscriptionEvidence =
            hasPrimaryStrongSignal ||
            hasStrongBodySignal;

        var blockedByMarketingSubject =
            subjectMarketing != null &&
            !hasPrimaryStrongSignal;

        result.IsSubscription =
            result.Score >= 5 &&
            hasRealSubscriptionEvidence &&
            !blockedByMarketingSubject;

        result.Confidence =
            !result.IsSubscription
                ? "Low"
                : result.Score switch
                {
                    >= 10 => "High",
                    >= 5 => "Medium",
                    _ => "Low"
                };

        return result;
    }

    // =========================================================
    // Phrase matcher
    // =========================================================

    private static string? FindFirstMatch(
        string text,
        IEnumerable<string> phrases)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return phrases.FirstOrDefault(
            phrase =>
                text.Contains(
                    phrase,
                    StringComparison.OrdinalIgnoreCase));
    }

    // =========================================================
    // Marketing detector
    //
    // Also catches phrases such as:
    // "save 40%"
    // "30% off"
    // =========================================================

    private static string? FindMarketingSignal(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var phrase =
            FindFirstMatch(
                text,
                MarketingPhrases);

        if (phrase != null)
        {
            return phrase;
        }

        var savePercentMatch =
            Regex.Match(
                text,
                @"\bsave\s+(?:up\s+to\s+)?\d{1,3}%\b",
                RegexOptions.IgnoreCase);

        if (savePercentMatch.Success)
        {
            return savePercentMatch.Value;
        }

        var percentOffMatch =
            Regex.Match(
                text,
                @"\b\d{1,3}%\s+off\b",
                RegexOptions.IgnoreCase);

        if (percentOffMatch.Success)
        {
            return percentOffMatch.Value;
        }

        return null;
    }

    // =========================================================
    // Billing period
    // =========================================================

    private static string? DetectBillingPeriod(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (MonthlyPhrases.Any(
            phrase =>
                text.Contains(
                    phrase,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return "Monthly";
        }

        if (YearlyPhrases.Any(
            phrase =>
                text.Contains(
                    phrase,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return "Yearly";
        }

        if (WeeklyPhrases.Any(
            phrase =>
                text.Contains(
                    phrase,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return "Weekly";
        }

        return null;
    }

    // =========================================================
    // Next billing date
    // =========================================================

    private static DateTime? ExtractNextBillingDate(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var patterns =
            new[]
            {
                @"(?:the\s+)?next\s+(?:charge|payment|billing)\s+(?:will\s+be\s+)?(?:on\s+)?(?<date>[A-Za-z]{3,9}\s+\d{1,2},\s+\d{4})",

                @"(?:you\s+)?will\s+be\s+charged\s+on\s+(?<date>[A-Za-z]{3,9}\s+\d{1,2},\s+\d{4})",

                @"renews\s+on\s+(?<date>[A-Za-z]{3,9}\s+\d{1,2},\s+\d{4})",

                @"renewal\s+date\s*(?:is|:)?\s*(?<date>[A-Za-z]{3,9}\s+\d{1,2},\s+\d{4})",

                @"next\s+billing\s+date\s*(?:is|:)?\s*(?<date>[A-Za-z]{3,9}\s+\d{1,2},\s+\d{4})",

                @"(?:the\s+)?next\s+(?:charge|payment|billing)\s+(?:will\s+be\s+)?(?:on\s+)?(?<date>\d{4}-\d{2}-\d{2})"
            };

        var formats =
            new[]
            {
                "MMM d, yyyy",
                "MMM dd, yyyy",
                "MMMM d, yyyy",
                "MMMM dd, yyyy",
                "yyyy-MM-dd"
            };

        foreach (var pattern in patterns)
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

            var dateText =
                match.Groups["date"]
                    .Value
                    .Trim();

            if (DateTime.TryParseExact(
                dateText,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
            {
                return DateTime.SpecifyKind(
                    parsedDate.Date,
                    DateTimeKind.Utc);
            }
        }

        return null;
    }

    // =========================================================
    // Amount extraction
    //
    // Priority:
    // "amount paid $20"
    // "total €19.99"
    // "charged $20"
    // then generic currency patterns
    // =========================================================

    private static decimal? ExtractAmount(
        string text,
        out string? currency)
    {
        currency = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var contextualPatterns =
            new[]
            {
                @"(?:amount\s+paid|total\s+paid|total|charged|charge|price)\s*:?\s*(?<currency>€|\$|£|eur|usd|gbp)\s*(?<amount>\d+(?:[.,]\d{1,2})?)",

                @"(?:amount\s+paid|total\s+paid|total|charged|charge|price)\s*:?\s*(?<amount>\d+(?:[.,]\d{1,2})?)\s*(?<currency>€|\$|£|eur|usd|gbp)"
            };

        foreach (var pattern in contextualPatterns)
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

            var amountText =
                match.Groups["amount"]
                    .Value
                    .Replace(',', '.');

            if (!decimal.TryParse(
                amountText,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var amount))
            {
                continue;
            }

            currency =
                NormalizeCurrency(
                    match.Groups["currency"].Value);

            return amount;
        }

        var genericPatterns =
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

        foreach (var currencyPatterns in genericPatterns)
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

    // =========================================================
    // Currency normalization
    // =========================================================

    private static string? NormalizeCurrency(
        string value)
    {
        return value
            .Trim()
            .ToUpperInvariant() switch
        {
            "€" => "EUR",
            "EUR" => "EUR",

            "$" => "USD",
            "USD" => "USD",

            "£" => "GBP",
            "GBP" => "GBP",

            _ => null
        };
    }

    // =========================================================
    // Merchant extraction
    // =========================================================

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
                return char.ToUpperInvariant(
                           domain[0]) +
                       domain[1..];
            }
        }

        return from;
    }
}