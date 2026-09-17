using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;
using Sub.Api.Models;
using Sub.Api.Services;
using Xunit;

namespace Sub.Api.Tests;

public class SubscriptionMatchingServiceTests
{
    [Fact]
    public async Task GenericMerchant_WithOneExistingSubscription_MatchesSafely()
    {
        await using var context =
            CreateContext();

        var existing =
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 1,
                Merchant = "Spotify",
                Status = "Active"
            };

        context.Subscriptions.Add(existing);

        await context.SaveChangesAsync();

        var service =
            new SubscriptionMatchingService(context);

        var result =
            await service.FindMatchingSubscriptionAsync(
                1,
                1,
                "Spotify",
                null,
                null,
                "Renewal");

        Assert.NotNull(result);

        Assert.Equal(
            existing.Id,
            result!.Id);
    }

    [Fact]
    public async Task GenericMerchant_WithMultipleSubscriptionsAndNoIdentity_DoesNotGuess()
    {
        await using var context =
            CreateContext();

        context.Subscriptions.AddRange(
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 1,
                Merchant = "Spotify",
                PlanName = "Individual",
                Status = "Active"
            },
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 1,
                Merchant = "Spotify",
                PlanName = "Family",
                Status = "Active"
            });

        await context.SaveChangesAsync();

        var service =
            new SubscriptionMatchingService(context);

        var result =
            await service.FindMatchingSubscriptionAsync(
                1,
                1,
                "Spotify",
                null,
                null,
                "Renewal");

        Assert.Null(result);
    }

    [Fact]
    public async Task Twitch_WithSubscriptionName_MatchesExactChannel()
    {
        await using var context =
            CreateContext();

        var lagmasterpiece =
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 1,
                Merchant = "Twitch",
                SubscriptionName = "lagmasterpiece",
                Status = "Active"
            };

        var anotherChannel =
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 1,
                Merchant = "Twitch",
                SubscriptionName = "anotherchannel",
                Status = "Active"
            };

        context.Subscriptions.AddRange(
            lagmasterpiece,
            anotherChannel);

        await context.SaveChangesAsync();

        var service =
            new SubscriptionMatchingService(context);

        var result =
            await service.FindMatchingSubscriptionAsync(
                1,
                1,
                "Twitch",
                "lagmasterpiece",
                null,
                "Cancellation");

        Assert.NotNull(result);

        Assert.Equal(
            lagmasterpiece.Id,
            result!.Id);
    }

    [Fact]
    public async Task TwitchActivation_WithGenericPlan_DoesNotMergeExistingSubscription()
    {
        await using var context =
            CreateContext();

        context.Subscriptions.Add(
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 1,
                Merchant = "Twitch",
                SubscriptionName = null,
                PlanName =
                    "Tier 1 - 1 Month Subscription - GR",
                Status = "Active"
            });

        await context.SaveChangesAsync();

        var service =
            new SubscriptionMatchingService(context);

        var result =
            await service.FindMatchingSubscriptionAsync(
                1,
                1,
                "Twitch",
                null,
                "Tier 1 - 1 Month Subscription - GR",
                "Activation");

        Assert.Null(result);
    }

    [Fact]
    public async Task TwitchRenewal_WithGenericPlan_MatchesSingleUnnamedSubscription()
    {
        await using var context =
            CreateContext();

        var existing =
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 1,
                Merchant = "Twitch",
                SubscriptionName = null,
                PlanName =
                    "Tier 1 - 1 Month Subscription - GR",
                Status = "Active"
            };

        context.Subscriptions.Add(existing);

        await context.SaveChangesAsync();

        var service =
            new SubscriptionMatchingService(context);

        var result =
            await service.FindMatchingSubscriptionAsync(
                1,
                1,
                "Twitch",
                null,
                "Tier 1 - 1 Month Subscription - GR",
                "Renewal");

        Assert.NotNull(result);

        Assert.Equal(
            existing.Id,
            result!.Id);
    }

    [Fact]
    public async Task Matching_DoesNotCrossUserBoundary()
    {
        await using var context =
            CreateContext();

        var userOneSubscription =
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 1,
                Merchant = "Spotify",
                Status = "Active"
            };

        var userTwoSubscription =
            new Subscription
            {
                UserId = 2,
                ConnectedEmailAccountId = 1,
                Merchant = "Spotify",
                Status = "Active"
            };

        context.Subscriptions.AddRange(
            userOneSubscription,
            userTwoSubscription);

        await context.SaveChangesAsync();

        var service =
            new SubscriptionMatchingService(context);

        var result =
            await service.FindMatchingSubscriptionAsync(
                1,
                1,
                "Spotify",
                null,
                null,
                "Renewal");

        Assert.NotNull(result);

        Assert.Equal(
            userOneSubscription.Id,
            result!.Id);

        Assert.NotEqual(
            userTwoSubscription.Id,
            result.Id);
    }

    [Fact]
    public async Task Matching_DoesNotCrossConnectedEmailAccountBoundary()
    {
        await using var context =
            CreateContext();

        var firstAccountSubscription =
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 1,
                Merchant = "Spotify",
                Status = "Active"
            };

        var secondAccountSubscription =
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 2,
                Merchant = "Spotify",
                Status = "Active"
            };

        context.Subscriptions.AddRange(
            firstAccountSubscription,
            secondAccountSubscription);

        await context.SaveChangesAsync();

        var service =
            new SubscriptionMatchingService(context);

        var result =
            await service.FindMatchingSubscriptionAsync(
                1,
                1,
                "Spotify",
                null,
                null,
                "Renewal");

        Assert.NotNull(result);

        Assert.Equal(
            firstAccountSubscription.Id,
            result!.Id);

        Assert.NotEqual(
            secondAccountSubscription.Id,
            result.Id);
    }
    [Fact]
    public async Task GenericSubscription_TrialToPaidToCancellation_MatchesSameSubscription()
    {
        await using var context =
            CreateContext();

        var service =
            new SubscriptionMatchingService(context);

        // =====================================================
        // 1. Trial starts
        // No existing subscription yet.
        // =====================================================

        var trialMatch =
            await service.FindMatchingSubscriptionAsync(
                1,
                1,
                "Canva",
                null,
                null,
                "Trial");

        Assert.Null(trialMatch);

        // Simulate the subscription created
        // after detecting the trial email.
        var subscription =
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 1,
                Merchant = "Canva",
                Status = "Active",
                BillingCycle = "Monthly",
                Amount = 0m,
                Currency = "EUR"
            };

        context.Subscriptions.Add(subscription);

        await context.SaveChangesAsync();

        // =====================================================
        // 2. Trial converts to paid subscription
        // =====================================================

        var activationMatch =
            await service.FindMatchingSubscriptionAsync(
                1,
                1,
                "Canva",
                null,
                null,
                "Activation");

        Assert.NotNull(activationMatch);

        Assert.Equal(
            subscription.Id,
            activationMatch!.Id);

        // =====================================================
        // 3. Subscription is later canceled
        // =====================================================

        var cancellationMatch =
            await service.FindMatchingSubscriptionAsync(
                1,
                1,
                "Canva",
                null,
                null,
                "Cancellation");

        Assert.NotNull(cancellationMatch);

        Assert.Equal(
            subscription.Id,
            cancellationMatch!.Id);

        Assert.Equal(
            activationMatch.Id,
            cancellationMatch.Id);
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