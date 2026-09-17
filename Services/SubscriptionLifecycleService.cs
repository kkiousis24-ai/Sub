using Sub.Api.Models;

namespace Sub.Api.Services;

public class SubscriptionLifecycleService
    : ISubscriptionLifecycleService
{
    public bool ApplyDetection(
        Subscription subscription,
        SubscriptionDetectionResult detection,
        bool canUpdateStatus)
    {
        var detectedStatus =
            GetDetectedStatus(detection);

        var wasCanceledNow = false;

        // =====================================================
        // Status + Next Billing Date
        // =====================================================

        if (canUpdateStatus)
        {
            var oldStatus =
                subscription.Status;

            subscription.Status =
                detectedStatus;

            if (
                detectedStatus == "Canceled" &&
                !oldStatus.Equals(
                    "Canceled",
                    StringComparison.OrdinalIgnoreCase))
            {
                wasCanceledNow = true;
            }

            if (detectedStatus == "Canceled")
            {
                subscription.NextBillingDate =
                    null;
            }
            else if (
                detection.NextBillingDate.HasValue)
            {
                subscription.NextBillingDate =
                    detection.NextBillingDate.Value;
            }
        }

        // =====================================================
        // Subscription Name
        //
        // Fill only when currently unknown.
        // =====================================================

        if (
            string.IsNullOrWhiteSpace(
                subscription.SubscriptionName) &&
            !string.IsNullOrWhiteSpace(
                detection.SubscriptionName))
        {
            subscription.SubscriptionName =
                detection.SubscriptionName.Trim();
        }

        // =====================================================
        // Plan Name
        //
        // Fill only when currently unknown.
        // =====================================================

        if (
            string.IsNullOrWhiteSpace(
                subscription.PlanName) &&
            !string.IsNullOrWhiteSpace(
                detection.PlanName))
        {
            subscription.PlanName =
                detection.PlanName.Trim();
        }

        // =====================================================
        // Amount
        // =====================================================

        if (
            detection.Amount.HasValue &&
            detection.Amount.Value > 0)
        {
            subscription.Amount =
                detection.Amount.Value;
        }

        // =====================================================
        // Currency
        // =====================================================

        if (!string.IsNullOrWhiteSpace(
            detection.Currency))
        {
            subscription.Currency =
                detection.Currency
                    .Trim()
                    .ToUpperInvariant();
        }

        // =====================================================
        // Billing Cycle
        // =====================================================

        if (
            !string.IsNullOrWhiteSpace(
                detection.BillingPeriod) &&
            !detection.BillingPeriod.Equals(
                "Unknown",
                StringComparison.OrdinalIgnoreCase))
        {
            subscription.BillingCycle =
                detection.BillingPeriod.Trim();
        }

        // =====================================================
        // Confidence
        // =====================================================

        var confidenceScore =
            Math.Min(
                detection.Score / 10.0,
                1.0);

        if (
            confidenceScore >
            subscription.ConfidenceScore)
        {
            subscription.ConfidenceScore =
                confidenceScore;
        }

        return wasCanceledNow;
    }

    private static string GetDetectedStatus(
        SubscriptionDetectionResult detection)
    {
        return detection.EventType switch
        {
            "Cancellation" =>
                "Canceled",

            "Activation" =>
                "Active",

            "Renewal" =>
                "Active",

            "Trial" =>
                "Active",

            _ =>
                detection.SubscriptionStatus switch
                {
                    "Canceled" =>
                        "Canceled",

                    "Active" =>
                        "Active",

                    _ =>
                        "Active"
                }
        };
    }
}