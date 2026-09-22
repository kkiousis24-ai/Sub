using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;
using Sub.Api.Models;
using Sub.Api.Services;
using Xunit;

namespace Sub.Api.Tests;

public class SubscriptionPipelineIntegrationTests
{
    [Fact]
    public async Task RenewalThenCancellation_UsesSameSubscriptionAndCancelsIt()
    {
        await using var context =
            CreateContext();

        var detectionService =
            new SubscriptionDetectionService();

        var matchingService =
            new SubscriptionMatchingService(context);

        var lifecycleService =
            new SubscriptionLifecycleService();

        // =====================================================
        // 1. Renewal email
        // =====================================================

        var renewalEmail =
            new EmailCandidate
            {
                GmailMessageId =
                    "spotify-renewal-pipeline-test",

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
                    """,

                Date =
                    new DateTime(
                        2026,
                        9,
                        20,
                        10,
                        0,
                        0,
                        DateTimeKind.Utc)
            };

        var renewalDetection =
            detectionService.Detect(
                renewalEmail);

        Assert.True(
            renewalDetection.IsSubscription);

        Assert.Equal(
            "Renewal",
            renewalDetection.EventType);

        Assert.Equal(
            "Spotify",
            renewalDetection.Merchant);

        // No subscription exists yet.
        var renewalMatch =
            await matchingService
                .FindMatchingSubscriptionAsync(
                    1,
                    1,
                    renewalDetection.Merchant!,
                    renewalDetection.SubscriptionName,
                    renewalDetection.PlanName,
                    renewalDetection.EventType);

        Assert.Null(
            renewalMatch);

        // Simulate the controller creating
        // the subscription.
        var subscription =
            new Subscription
            {
                UserId = 1,

                ConnectedEmailAccountId = 1,

                Merchant =
                    renewalDetection.Merchant!,

                SubscriptionName =
                    renewalDetection.SubscriptionName,

                PlanName =
                    renewalDetection.PlanName,

                Amount =
                    renewalDetection.Amount ??
                    0m,

                Currency =
                    renewalDetection.Currency ??
                    "EUR",

                BillingCycle =
                    renewalDetection.BillingPeriod ??
                    "Unknown",

                NextBillingDate =
                    renewalDetection.NextBillingDate,

                Status = "Active",

                ConfidenceScore =
                    Math.Min(
                        renewalDetection.Score / 10.0,
                        1.0),

                CreatedAt =
                    DateTime.UtcNow
            };

        context.Subscriptions.Add(
            subscription);

        await context.SaveChangesAsync();

        Assert.NotNull(
            subscription.NextBillingDate);

        // =====================================================
        // 2. Cancellation email
        // =====================================================

        var cancellationEmail =
            new EmailCandidate
            {
                GmailMessageId =
                    "spotify-cancellation-pipeline-test",

                From =
                    "Spotify <no-reply@spotify.com>",

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
                    """,

                Date =
                    new DateTime(
                        2026,
                        9,
                        25,
                        10,
                        0,
                        0,
                        DateTimeKind.Utc)
            };

        var cancellationDetection =
            detectionService.Detect(
                cancellationEmail);

        Assert.True(
            cancellationDetection.IsSubscription);

        Assert.Equal(
            "Cancellation",
            cancellationDetection.EventType);

        Assert.Equal(
            "Spotify",
            cancellationDetection.Merchant);

        // =====================================================
        // 3. Matching
        // =====================================================

        var cancellationMatch =
            await matchingService
                .FindMatchingSubscriptionAsync(
                    1,
                    1,
                    cancellationDetection.Merchant!,
                    cancellationDetection.SubscriptionName,
                    cancellationDetection.PlanName,
                    cancellationDetection.EventType);

        Assert.NotNull(
            cancellationMatch);

        Assert.Equal(
            subscription.Id,
            cancellationMatch!.Id);

        // =====================================================
        // 4. Lifecycle update
        // =====================================================

        var wasCanceledNow =
            lifecycleService.ApplyDetection(
                cancellationMatch,
                cancellationDetection,
                true);

        await context.SaveChangesAsync();

        // =====================================================
        // 5. Final validation
        // =====================================================

        Assert.True(
            wasCanceledNow);

        Assert.Equal(
            "Canceled",
            cancellationMatch.Status);

        Assert.Null(
            cancellationMatch.NextBillingDate);

        Assert.Equal(
            1,
            await context.Subscriptions.CountAsync());
    }
    [Fact]
    public async Task TrialThenPaidThenCancellation_UsesSameSubscription()
    {
        await using var context =
            CreateContext();

        var detectionService =
            new SubscriptionDetectionService();

        var matchingService =
            new SubscriptionMatchingService(context);

        var lifecycleService =
            new SubscriptionLifecycleService();

        // =====================================================
        // 1. Trial
        // =====================================================

        var trialEmail =
            new EmailCandidate
            {
                GmailMessageId =
                    "canva-trial-pipeline-test",

                From =
                    "Canva <no-reply@canva.com>",

                Subject =
                    "Your trial has started",

                Snippet =
                    "Your trial has started successfully.",

                BodyText =
                    """
                    Your trial has started.

                    Enjoy your premium trial.
                    """,

                Date =
                    new DateTime(
                        2026,
                        9,
                        1,
                        10,
                        0,
                        0,
                        DateTimeKind.Utc)
            };

        var trialDetection =
            detectionService.Detect(
                trialEmail);

        Assert.True(
            trialDetection.IsSubscription);

        Assert.Equal(
            "Trial",
            trialDetection.EventType);

        Assert.Equal(
            "Canva",
            trialDetection.Merchant);

        var trialMatch =
            await matchingService
                .FindMatchingSubscriptionAsync(
                    1,
                    1,
                    trialDetection.Merchant!,
                    trialDetection.SubscriptionName,
                    trialDetection.PlanName,
                    trialDetection.EventType);

        Assert.Null(
            trialMatch);

        var subscription =
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 1,
                Merchant = "Canva",
                Status = "Active",
                Amount = 0m,
                Currency = "EUR",
                BillingCycle = "Unknown",
                CreatedAt = DateTime.UtcNow
            };

        context.Subscriptions.Add(
            subscription);

        await context.SaveChangesAsync();

        // =====================================================
        // 2. Trial becomes paid
        // =====================================================

        var activationEmail =
            new EmailCandidate
            {
                GmailMessageId =
                    "canva-activation-pipeline-test",

                From =
                    "Canva <no-reply@canva.com>",

                Subject =
                    "Your subscription is confirmed",

                Snippet =
                    "Your monthly subscription is now active.",

                BodyText =
                    """
                    Your subscription is confirmed.

                    Your monthly subscription is now active.

                    Total paid: €12.99

                    Next billing date is Oct 15, 2026.
                    """,

                Date =
                    new DateTime(
                        2026,
                        9,
                        15,
                        10,
                        0,
                        0,
                        DateTimeKind.Utc)
            };

        var activationDetection =
            detectionService.Detect(
                activationEmail);

        Assert.True(
            activationDetection.IsSubscription);

        Assert.Equal(
            "Activation",
            activationDetection.EventType);

        var activationMatch =
            await matchingService
                .FindMatchingSubscriptionAsync(
                    1,
                    1,
                    activationDetection.Merchant!,
                    activationDetection.SubscriptionName,
                    activationDetection.PlanName,
                    activationDetection.EventType);

        Assert.NotNull(
            activationMatch);

        Assert.Equal(
            subscription.Id,
            activationMatch!.Id);

        lifecycleService.ApplyDetection(
            activationMatch,
            activationDetection,
            true);

        await context.SaveChangesAsync();

        Assert.Equal(
            12.99m,
            activationMatch.Amount);

        Assert.Equal(
            "Monthly",
            activationMatch.BillingCycle);

        Assert.Equal(
            "Active",
            activationMatch.Status);

        Assert.NotNull(
            activationMatch.NextBillingDate);

        // =====================================================
        // 3. Cancellation
        // =====================================================

        var cancellationEmail =
            new EmailCandidate
            {
                GmailMessageId =
                    "canva-cancellation-pipeline-test",

                From =
                    "Canva <no-reply@canva.com>",

                Subject =
                    "Your subscription has been canceled",

                Snippet =
                    "Your subscription has been canceled.",

                BodyText =
                    """
                    Your subscription has been canceled.

                    Your subscription will not auto-renew.
                    """,

                Date =
                    new DateTime(
                        2026,
                        9,
                        25,
                        10,
                        0,
                        0,
                        DateTimeKind.Utc)
            };

        var cancellationDetection =
            detectionService.Detect(
                cancellationEmail);

        Assert.True(
            cancellationDetection.IsSubscription);

        Assert.Equal(
            "Cancellation",
            cancellationDetection.EventType);

        var cancellationMatch =
            await matchingService
                .FindMatchingSubscriptionAsync(
                    1,
                    1,
                    cancellationDetection.Merchant!,
                    cancellationDetection.SubscriptionName,
                    cancellationDetection.PlanName,
                    cancellationDetection.EventType);

        Assert.NotNull(
            cancellationMatch);

        Assert.Equal(
            subscription.Id,
            cancellationMatch!.Id);

        var wasCanceledNow =
            lifecycleService.ApplyDetection(
                cancellationMatch,
                cancellationDetection,
                true);

        await context.SaveChangesAsync();

        // =====================================================
        // 4. Final validation
        // =====================================================

        Assert.True(
            wasCanceledNow);

        Assert.Equal(
            "Canceled",
            cancellationMatch.Status);

        Assert.Equal(
            12.99m,
            cancellationMatch.Amount);

        Assert.Null(
            cancellationMatch.NextBillingDate);

        Assert.Equal(
            1,
            await context.Subscriptions.CountAsync());
    }

    private static AppDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(
                    "Data Source=:memory:")
                .Options;

        var context =
            new AppDbContext(options);

        context.Database.OpenConnection();

        context.Database.EnsureCreated();

        return context;
    }
}