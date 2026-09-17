using Sub.Api.Models;
using Sub.Api.Services;
using Xunit;

namespace Sub.Api.Tests;

public class GenericSubscriptionDetectionTests
{
    [Fact]
    public void Detect_GenericMonthlyRenewal_CreatesSubscription()
    {
        var service =
            new SubscriptionDetectionService();

        var email =
            new EmailCandidate
            {
                GmailMessageId =
                    "generic-monthly-renewal-test",

                From =
                    "Spotify <no-reply@spotify.com>",

                Subject =
                    "Your subscription will automatically renew",

                Snippet =
                    "Your subscription will automatically renew " +
                    "on Oct 3, 2026 for €10.99.",

                BodyText =
                    """
                    Your subscription will automatically renew.

                    Your monthly subscription will renew
                    on Oct 3, 2026.

                    You will be charged €10.99
                    to your payment method.

                    Thank you.
                    """,

                Date =
                    new DateTime(
                        2026,
                        9,
                        17,
                        10,
                        0,
                        0,
                        DateTimeKind.Utc)
            };

        var result =
            service.Detect(email);

        Assert.True(
            result.IsSubscription);

        Assert.Equal(
            "Renewal",
            result.EventType);

        Assert.Equal(
            "Active",
            result.SubscriptionStatus);

        Assert.Equal(
            10.99m,
            result.Amount);

        Assert.Equal(
            "EUR",
            result.Currency);

        Assert.Equal(
            "Monthly",
            result.BillingPeriod);

        Assert.NotNull(
            result.NextBillingDate);

        Assert.Equal(
            new DateTime(2026, 10, 3),
            result.NextBillingDate!.Value.Date);
    }

    [Fact]
    public void Detect_GenericCancellation_CreatesCanceledSubscriptionEvent()
    {
        var service =
            new SubscriptionDetectionService();

        var email =
            new EmailCandidate
            {
                GmailMessageId =
                    "generic-cancellation-test",

                From =
                    "Dropbox <no-reply@dropbox.com>",

                Subject =
                    "Your subscription has been canceled",

                Snippet =
                    "Your subscription has been canceled " +
                    "and will not auto-renew.",

                BodyText =
                    """
                    Your subscription has been canceled.

                    Your subscription will remain active
                    until the end of your current billing period.

                    After that date,
                    your subscription will not auto-renew.

                    Thank you.
                    """,

                Date =
                    new DateTime(
                        2026,
                        9,
                        17,
                        11,
                        0,
                        0,
                        DateTimeKind.Utc)
            };

        var result =
            service.Detect(email);

        Assert.True(
            result.IsSubscription);

        Assert.Equal(
            "Cancellation",
            result.EventType);

        Assert.Equal(
            "Canceled",
            result.SubscriptionStatus);

        Assert.True(
            result.Score >= 5);
    }

    [Fact]
    public void Detect_GenericActivation_CreatesActiveSubscription()
    {
        var service =
            new SubscriptionDetectionService();

        var email =
            new EmailCandidate
            {
                GmailMessageId =
                    "generic-activation-test",

                From =
                    "Adobe <no-reply@adobe.com>",

                Subject =
                    "Your subscription is confirmed",

                Snippet =
                    "Your subscription is confirmed and is now active.",

                BodyText =
                    """
                    Your subscription is confirmed.

                    Your membership is now active.

                    Thank you for subscribing.

                    Your monthly plan will continue
                    until you cancel.

                    Total paid: €12.99
                    """,

                Date =
                    new DateTime(
                        2026,
                        9,
                        17,
                        12,
                        0,
                        0,
                        DateTimeKind.Utc)
            };

        var result =
            service.Detect(email);

        Assert.True(
            result.IsSubscription);

        Assert.Equal(
            "Activation",
            result.EventType);

        Assert.Equal(
            "Active",
            result.SubscriptionStatus);

        Assert.Equal(
            12.99m,
            result.Amount);

        Assert.Equal(
            "EUR",
            result.Currency);

        Assert.Equal(
            "Monthly",
            result.BillingPeriod);

        Assert.True(
            result.Score >= 5);
    }

    [Fact]
    public void Detect_GenericYearlySubscription_DetectsYearlyBilling()
    {
        var service =
            new SubscriptionDetectionService();

        var email =
            new EmailCandidate
            {
                GmailMessageId =
                    "generic-yearly-subscription-test",

                From =
                    "Notion <team@notion.so>",

                Subject =
                    "Your annual subscription will renew soon",

                Snippet =
                    "Your annual subscription will renew " +
                    "on Sep 30, 2026 for €96.00.",

                BodyText =
                    """
                    Your annual subscription will renew soon.

                    Your subscription renews yearly.

                    You will be charged €96.00
                    on Sep 30, 2026.

                    Thank you for being a subscriber.
                    """,

                Date =
                    new DateTime(
                        2026,
                        9,
                        17,
                        13,
                        0,
                        0,
                        DateTimeKind.Utc)
            };

        var result =
            service.Detect(email);

        Assert.True(
            result.IsSubscription);

        Assert.Equal(
            "Renewal",
            result.EventType);

        Assert.Equal(
            "Active",
            result.SubscriptionStatus);

        Assert.Equal(
            96.00m,
            result.Amount);

        Assert.Equal(
            "EUR",
            result.Currency);

        Assert.Equal(
            "Yearly",
            result.BillingPeriod);

        Assert.NotNull(
            result.NextBillingDate);

        Assert.Equal(
            new DateTime(2026, 9, 30),
            result.NextBillingDate!.Value.Date);

        Assert.True(
            result.Score >= 5);
    }
}