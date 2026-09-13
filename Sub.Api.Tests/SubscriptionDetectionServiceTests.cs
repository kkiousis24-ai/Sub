using Sub.Api.Models;
using Sub.Api.Services;
using Xunit;

namespace Sub.Api.Tests;

public class SubscriptionDetectionServiceTests
{
    [Fact]
    public void Detect_TwitchCancellation_ExtractsSubscriptionName()
    {
        var service =
            new SubscriptionDetectionService();

        var email =
            new EmailCandidate
            {
                GmailMessageId =
                    "1938315d2eeb251e",

                From =
                    "purchase-noreply@twitch.tv",

                Subject =
                    "Your lagmasterpiece Subscription Cancellation Confirmation",

                Snippet =
                    "Canceled Subscription Confirmation. " +
                    "This email confirms your recent cancellation " +
                    "to lagmasterpiece. " +
                    "Your subscription has been canceled.",

                BodyText =
                    """
                    Canceled Subscription Confirmation

                    Hi KONSTANTINOS NIKOLAKOPOULOS,

                    This email confirms your recent cancellation to lagmasterpiece.
                    As of the date of this email, your subscription has been canceled
                    and will not auto-renew.

                    You will continue to enjoy your subscription benefits
                    until Jan 1, 2025.

                    Thank you,
                    Twitch
                    """,

                Date =
                    new DateTime(
                        2024,
                        12,
                        1,
                        16,
                        37,
                        45,
                        DateTimeKind.Utc)
            };

        var result =
            service.Detect(email);

        Assert.True(
            result.IsSubscription);

        Assert.Equal(
            "Twitch",
            result.Merchant);

        Assert.Equal(
            "lagmasterpiece",
            result.SubscriptionName);

        Assert.Null(
            result.PlanName);

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
    public void Detect_TwitchActivation_ExtractsPlanAndBillingDetails()
    {
        var service =
            new SubscriptionDetectionService();

        var email =
            new EmailCandidate
            {
                GmailMessageId =
                    "1946bda2ff26f587",

                From =
                    "purchase-noreply@twitch.tv",

                Subject =
                    "Thanks for Subscribing, KONSTANTINOS NIKOLAKOPOULOS",

                Snippet =
                    "Subscription Purchase Confirmation " +
                    "Order Number #603611083. " +
                    "This email confirms your recent payment of €4.99 " +
                    "on Jan 15, 2025.",

                BodyText =
                    """
                    Thank you for subscribing.

                    This email confirms your recent payment.

                    Your subscription is now active and will automatically renew.

                    Invoice #603611083

                    KONSTANTINOS NIKOLAKOPOULOS
                    Greece

                    Your Plan: Tier 1 - 1 Month Subscription - GR
                    Next Invoice: Feb 15, 2025
                    Total Paid: €4.99

                    Charged to MasterCard: €4.99

                    €4.02 -- Tier 1 - 1 Month Subscription - GR
                    Jan 15 – Feb 15, 2025

                    Subtotal: €4.02
                    GR VAT 24%

                    Total: €4.99
                    Paid: -€4.99
                    Total Due: €0.00
                    """,

                Date =
                    new DateTime(
                        2025,
                        1,
                        15,
                        21,
                        24,
                        8,
                        DateTimeKind.Utc)
            };

        var result =
            service.Detect(email);

        Assert.True(
            result.IsSubscription);

        Assert.Equal(
            "Twitch",
            result.Merchant);

        Assert.Null(
            result.SubscriptionName);

        Assert.Equal(
            "Tier 1 - 1 Month Subscription - GR",
            result.PlanName);

        Assert.Equal(
            "Activation",
            result.EventType);

        Assert.Equal(
            "Active",
            result.SubscriptionStatus);

        Assert.Equal(
            4.99m,
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
            new DateTime(2025, 2, 15),
            result.NextBillingDate!.Value.Date);

        Assert.True(
            result.Score >= 5);
    }

    [Fact]
    public void Detect_TwitchRenewal_ExtractsPlanAndNextBillingDate()
    {
        var service =
            new SubscriptionDetectionService();

        var email =
            new EmailCandidate
            {
                GmailMessageId =
                    "19b9784009f04919",

                From =
                    "purchase-noreply@twitch.tv",

                Subject =
                    "Your Subscription Will Be Renewing Soon",

                Snippet =
                    "Your Subscription Will Be Renewing Soon. " +
                    "Thank you for being a Twitch subscriber! " +
                    "Your subscription will automatically renew on 6 Feb 2026.",

                BodyText =
                    """
                    Your subscription is about to renew.

                    This is just a reminder that your subscription to
                    Tier 1 - 1 Month Subscription - GR
                    will invoice automatically on 6 Feb 2026.

                    If you have any questions,
                    please contact us at purchase-noreply@twitch.tv.

                    Thank you,
                    Twitch Interactive Germany GmbH
                    """,

                Date =
                    new DateTime(
                        2026,
                        1,
                        7,
                        8,
                        12,
                        49,
                        DateTimeKind.Utc)
            };

        var result =
            service.Detect(email);

        Assert.True(
            result.IsSubscription);

        Assert.Equal(
            "Twitch",
            result.Merchant);

        Assert.Null(
            result.SubscriptionName);

        Assert.Equal(
            "Tier 1 - 1 Month Subscription - GR",
            result.PlanName);

        Assert.Equal(
            "Renewal",
            result.EventType);

        Assert.Equal(
            "Monthly",
            result.BillingPeriod);

        Assert.NotNull(
            result.NextBillingDate);

        Assert.Equal(
            new DateTime(2026, 2, 6),
            result.NextBillingDate!.Value.Date);

        Assert.True(
            result.Score >= 5);
    }

    [Fact]
    public void Detect_CourseraMarketingEmail_DoesNotCreateSubscription()
    {
        var service =
            new SubscriptionDetectionService();

        var email =
            new EmailCandidate
            {
                GmailMessageId =
                    "19dabb7bd339240c",

                From =
                    "Coursera <Coursera@m.learn.coursera.org>",

                Subject =
                    "Ready: Fundamentals of Business Finance, " +
                    "with Goldman Sachs 10,000 Women",

                Snippet =
                    "Ready to learn something new?",

                BodyText =
                    """
                    Unlock 10,000+ courses risk-free for 7 days.
                    Start free trial.

                    Coursera Plus free trials are limited
                    to one per user.

                    If you do not cancel during the free trial period,
                    you will be charged $59 USD or equivalent
                    in local currency after the 7-day free trial
                    and monthly thereafter.

                    Cancel anytime in account settings.
                    """,

                Date =
                    new DateTime(
                        2026,
                        4,
                        20,
                        16,
                        27,
                        18,
                        DateTimeKind.Utc)
            };

        var result =
            service.Detect(email);

        Assert.False(
            result.IsSubscription);

        Assert.Equal(
            "Unknown",
            result.EventType);

        Assert.Equal(
            "Low",
            result.Confidence);
    }

    [Fact]
    public void Detect_NetflixAccountSecurityEmail_DoesNotCreateSubscription()
    {
        var service =
            new SubscriptionDetectionService();

        var email =
            new EmailCandidate
            {
                GmailMessageId =
                    "1971c25ab5e2fd7c",

                From =
                    "Netflix <info@account.netflix.com>",

                Subject =
                    "A new device is using your account",

                Snippet =
                    "Please review who's using your Netflix account.",

                BodyText =
                    """
                    Please review who's using your Netflix account.

                    A new device is using your account.

                    Hi Kostas,

                    A new device signed in to your Netflix account.

                    The details

                    Device
                    iPhone Safari - Mobile Browser

                    Location
                    West Greece, Greece

                    If this was you or someone in your household:
                    Enjoy watching!

                    If it was someone else:
                    We recommend that you change your password
                    immediately to keep your account secure.

                    The Netflix team

                    This message was mailed to you
                    by Netflix as part of your Netflix membership.
                    """,

                Date =
                    new DateTime(
                        2025,
                        5,
                        29,
                        13,
                        5,
                        19,
                        DateTimeKind.Utc)
            };

        var result =
            service.Detect(email);

        Assert.False(
            result.IsSubscription);

        Assert.Equal(
            "Unknown",
            result.EventType);

        Assert.Equal(
            "Low",
            result.Confidence);

        Assert.Null(
            result.SubscriptionName);

        Assert.Null(
            result.PlanName);
    }

    [Fact]
    public void Detect_SkroutzReceipt_DoesNotCreateSubscription()
    {
        var service =
            new SubscriptionDetectionService();

        var email =
            new EmailCandidate
            {
                GmailMessageId =
                    "19e8da45c30cd03e",

                From =
                    "Skroutz <ecommerce-support@skroutz.gr>",

                Subject =
                    "#260601-4702668 | Η απόδειξή σου είναι διαθέσιμη",

                Snippet =
                    "Η απόδειξή σου από MG Manager είναι διαθέσιμη! " +
                    "Κωδικός παραγγελίας: 260601-4702668.",

                BodyText =
                    """
                    Η απόδειξή σου από MG Manager είναι διαθέσιμη!

                    Κωδικός παραγγελίας:
                    260601-4702668

                    Απόδειξη παραγγελίας 260601-4702668

                    Η απόδειξή σου έχει εκδοθεί
                    και την έχουμε επισυνάψει σε αυτό το email.

                    Xiaomi Redmi Note 14 4G NFC Dual SIM
                    (8/256GB) Midnight Black

                    Από MG Manager

                    Δες την παραγγελία σου.

                    Copyright © 2026 Skroutz S.A.
                    All Rights Reserved.
                    """,

                Date =
                    new DateTime(
                        2026,
                        6,
                        3,
                        13,
                        20,
                        17,
                        DateTimeKind.Utc)
            };

        var result =
            service.Detect(email);

        Assert.False(
            result.IsSubscription);

        Assert.Equal(
            "Unknown",
            result.EventType);

        Assert.Equal(
            "Low",
            result.Confidence);

        Assert.Null(
            result.SubscriptionName);

        Assert.Null(
            result.PlanName);
    }
}