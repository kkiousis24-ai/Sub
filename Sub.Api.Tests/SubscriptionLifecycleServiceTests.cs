using Sub.Api.Models;
using Sub.Api.Services;
using Xunit;

namespace Sub.Api.Tests;

public class SubscriptionLifecycleServiceTests
{
    [Fact]
    public void ApplyDetection_Activation_UpdatesSubscriptionData()
    {
        var subscription =
            new Subscription
            {
                Status = "Active",
                Amount = 0m,
                Currency = "EUR",
                BillingCycle = "Unknown",
                ConfidenceScore = 0.2
            };

        var detection =
            new SubscriptionDetectionResult
            {
                EventType = "Activation",
                Amount = 12.99m,
                Currency = "eur",
                BillingPeriod = "Monthly",
                NextBillingDate =
                    new DateTime(2026, 10, 30),
                Score = 8
            };

        var service =
            new SubscriptionLifecycleService();

        var wasCanceledNow =
            service.ApplyDetection(
                subscription,
                detection,
                true);

        Assert.False(wasCanceledNow);

        Assert.Equal(
            "Active",
            subscription.Status);

        Assert.Equal(
            12.99m,
            subscription.Amount);

        Assert.Equal(
            "EUR",
            subscription.Currency);

        Assert.Equal(
            "Monthly",
            subscription.BillingCycle);

        Assert.Equal(
            new DateTime(2026, 10, 30),
            subscription.NextBillingDate);

        Assert.Equal(
            0.8,
            subscription.ConfidenceScore,
            3);
    }

    [Fact]
    public void ApplyDetection_Cancellation_ClearsNextBillingDate()
    {
        var subscription =
            new Subscription
            {
                Status = "Active",
                Amount = 12.99m,
                Currency = "EUR",
                BillingCycle = "Monthly",
                NextBillingDate =
                    new DateTime(2026, 10, 30)
            };

        var detection =
            new SubscriptionDetectionResult
            {
                EventType = "Cancellation",
                Score = 9
            };

        var service =
            new SubscriptionLifecycleService();

        var wasCanceledNow =
            service.ApplyDetection(
                subscription,
                detection,
                true);

        Assert.True(wasCanceledNow);

        Assert.Equal(
            "Canceled",
            subscription.Status);

        Assert.Null(
            subscription.NextBillingDate);
    }

    [Fact]
    public void ApplyDetection_OlderEmail_DoesNotOverwriteStatusOrNextBillingDate()
    {
        var originalNextBillingDate =
            new DateTime(2026, 11, 15);

        var subscription =
            new Subscription
            {
                Status = "Active",
                NextBillingDate =
                    originalNextBillingDate
            };

        var detection =
            new SubscriptionDetectionResult
            {
                EventType = "Cancellation",
                NextBillingDate =
                    new DateTime(2026, 1, 1),
                Score = 7
            };

        var service =
            new SubscriptionLifecycleService();

        var wasCanceledNow =
            service.ApplyDetection(
                subscription,
                detection,
                false);

        Assert.False(wasCanceledNow);

        Assert.Equal(
            "Active",
            subscription.Status);

        Assert.Equal(
            originalNextBillingDate,
            subscription.NextBillingDate);
    }

    [Fact]
    public void ApplyDetection_FillsMissingIdentityButDoesNotOverwriteExistingIdentity()
    {
        var subscription =
            new Subscription
            {
                SubscriptionName = null,
                PlanName = "Existing Plan"
            };

        var detection =
            new SubscriptionDetectionResult
            {
                SubscriptionName = "Premium Account",
                PlanName = "New Plan",
                EventType = "Renewal"
            };

        var service =
            new SubscriptionLifecycleService();

        service.ApplyDetection(
            subscription,
            detection,
            true);

        Assert.Equal(
            "Premium Account",
            subscription.SubscriptionName);

        Assert.Equal(
            "Existing Plan",
            subscription.PlanName);
    }

    [Fact]
    public void ApplyDetection_LowerConfidence_DoesNotReduceExistingConfidence()
    {
        var subscription =
            new Subscription
            {
                ConfidenceScore = 0.9
            };

        var detection =
            new SubscriptionDetectionResult
            {
                EventType = "Renewal",
                Score = 5
            };

        var service =
            new SubscriptionLifecycleService();

        service.ApplyDetection(
            subscription,
            detection,
            true);

        Assert.Equal(
            0.9,
            subscription.ConfidenceScore,
            3);
    }
}