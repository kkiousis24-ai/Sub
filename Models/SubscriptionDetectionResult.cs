namespace Sub.Api.Models;

public class SubscriptionDetectionResult
{
    public bool IsSubscription { get; set; }

    public int Score { get; set; }

    public string Confidence { get; set; } = "Low";

    public string? Merchant { get; set; }

    public string? PlanName { get; set; }

    // Activation / Renewal / Cancellation / Trial / Unknown
    public string EventType { get; set; } = "Unknown";

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }

    public string? BillingPeriod { get; set; }

    public DateTime? NextBillingDate { get; set; }

    // Active / Canceled / Unknown
    public string SubscriptionStatus { get; set; } = "Unknown";

    public List<string> Reasons { get; set; } = new();
}