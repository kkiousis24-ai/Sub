using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;
using Sub.Api.Models;

namespace Sub.Api.Services;

public class SubscriptionMatchingService
    : ISubscriptionMatchingService
{
    private readonly AppDbContext _context;

    public SubscriptionMatchingService(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task<Subscription?>
        FindMatchingSubscriptionAsync(
            int userId,
            int connectedEmailAccountId,
            string merchant,
            string? detectedSubscriptionName,
            string? detectedPlanName,
            string eventType)
    {
        var normalizedMerchant =
            merchant
                .Trim()
                .ToLowerInvariant();

        var merchantSubscriptions =
            await _context.Subscriptions
                .Where(s =>
                    s.UserId ==
                    userId &&

                    s.ConnectedEmailAccountId ==
                    connectedEmailAccountId &&

                    s.Merchant.ToLower() ==
                    normalizedMerchant)
                .ToListAsync();

        if (merchantSubscriptions.Count == 0)
        {
            return null;
        }

        // =====================================================
        // TWITCH
        // =====================================================

        if (merchant.Equals(
            "Twitch",
            StringComparison.OrdinalIgnoreCase))
        {
            // -------------------------------------------------
            // Specific Twitch channel identity
            // -------------------------------------------------

            if (!string.IsNullOrWhiteSpace(
                detectedSubscriptionName))
            {
                var nameMatches =
                    merchantSubscriptions
                        .Where(s =>
                            SubscriptionNamesEqual(
                                s.SubscriptionName,
                                detectedSubscriptionName))
                        .ToList();

                if (nameMatches.Count == 1)
                {
                    return nameMatches[0];
                }

                return null;
            }

            // -------------------------------------------------
            // Generic Twitch plan only
            // -------------------------------------------------

            if (!string.IsNullOrWhiteSpace(
                detectedPlanName))
            {
                var planOnlyMatches =
                    merchantSubscriptions
                        .Where(s =>
                            string.IsNullOrWhiteSpace(
                                s.SubscriptionName) &&

                            PlanNamesEqual(
                                s.PlanName,
                                detectedPlanName))
                        .ToList();

                // Activation may represent
                // a completely new Twitch channel.
                if (eventType.Equals(
                    "Activation",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                if (planOnlyMatches.Count == 1)
                {
                    return planOnlyMatches[0];
                }

                return null;
            }

            return null;
        }

        // =====================================================
        // OTHER MERCHANTS
        // =====================================================

        if (!string.IsNullOrWhiteSpace(
            detectedSubscriptionName))
        {
            var nameMatches =
                merchantSubscriptions
                    .Where(s =>
                        SubscriptionNamesEqual(
                            s.SubscriptionName,
                            detectedSubscriptionName))
                    .ToList();

            if (nameMatches.Count == 1)
            {
                return nameMatches[0];
            }

            return null;
        }

        if (!string.IsNullOrWhiteSpace(
            detectedPlanName))
        {
            var planMatches =
                merchantSubscriptions
                    .Where(s =>
                        PlanNamesEqual(
                            s.PlanName,
                            detectedPlanName))
                    .ToList();

            if (planMatches.Count == 1)
            {
                return planMatches[0];
            }

            return null;
        }

        // Merchant-only fallback
        // is safe only when exactly one exists.
        if (merchantSubscriptions.Count == 1)
        {
            return merchantSubscriptions[0];
        }

        return null;
    }

    private static bool SubscriptionNamesEqual(
        string? first,
        string? second)
    {
        if (
            string.IsNullOrWhiteSpace(first) ||
            string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        return first.Trim().Equals(
            second.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool PlanNamesEqual(
        string? first,
        string? second)
    {
        if (
            string.IsNullOrWhiteSpace(first) ||
            string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        return first.Trim().Equals(
            second.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }
}