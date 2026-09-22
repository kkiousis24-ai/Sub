using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;
using Sub.Api.Models;
using Sub.Api.Services;
using Xunit;

namespace Sub.Api.Tests;

public class SubscriptionEvidenceServiceTests
{
    [Fact]
    public async Task ExistingEvidence_ForSameAccount_ReturnsTrue()
    {
        await using var context =
            CreateContext();

        var subscription =
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 1,
                Merchant = "Spotify"
            };

        context.Subscriptions.Add(subscription);

        await context.SaveChangesAsync();

        var evidence =
            new SubscriptionEvidence
            {
                SubscriptionId = subscription.Id,
                MessageId = "message-123",
                Sender = "test@example.com",
                Subject = "Subscription email"
            };

        context.SubscriptionEvidences.Add(evidence);

        await context.SaveChangesAsync();

        var service =
            new SubscriptionEvidenceService(context);

        var result =
            await service.EvidenceExistsAsync(
                1,
                "message-123");

        Assert.True(result);
    }

    [Fact]
    public async Task SameMessageId_ForDifferentAccount_ReturnsFalse()
    {
        await using var context =
            CreateContext();

        var subscription =
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 2,
                Merchant = "Spotify"
            };

        context.Subscriptions.Add(subscription);

        await context.SaveChangesAsync();

        var evidence =
            new SubscriptionEvidence
            {
                SubscriptionId = subscription.Id,
                MessageId = "message-123",
                Sender = "test@example.com",
                Subject = "Subscription email"
            };

        context.SubscriptionEvidences.Add(evidence);

        await context.SaveChangesAsync();

        var service =
            new SubscriptionEvidenceService(context);

        var result =
            await service.EvidenceExistsAsync(
                1,
                "message-123");

        Assert.False(result);
    }

    [Fact]
    public async Task UnsavedEvidence_InCurrentScan_ReturnsTrue()
    {
        await using var context =
            CreateContext();

        var subscription =
            new Subscription
            {
                UserId = 1,
                ConnectedEmailAccountId = 1,
                Merchant = "Spotify"
            };

        context.Subscriptions.Add(subscription);

        await context.SaveChangesAsync();

        var evidence =
            new SubscriptionEvidence
            {
                SubscriptionId = subscription.Id,
                MessageId = "message-123",
                Sender = "test@example.com",
                Subject = "Subscription email"
            };

        context.SubscriptionEvidences.Add(evidence);

        var service =
            new SubscriptionEvidenceService(context);

        var result =
            await service.EvidenceExistsAsync(
                1,
                "message-123");

        Assert.True(result);
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