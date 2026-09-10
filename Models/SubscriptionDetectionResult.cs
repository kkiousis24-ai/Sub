namespace Sub.Api.Models;

public class SubscriptionDetectionResult
{
    public bool IsSubscription { get; set; }

    public int Score { get; set; }

    public string Confidence { get; set; } = "Low";

    public string? Merchant { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }

    public string? BillingPeriod { get; set; }

    public List<string> Reasons { get; set; } = new();
}