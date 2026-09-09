namespace Sub.Api.Models;

public class Subscription
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int ConnectedEmailAccountId { get; set; }

    public string Merchant { get; set; } = string.Empty;

    public string? PlanName { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";

    public string BillingCycle { get; set; } = string.Empty;

    public DateTime? NextBillingDate { get; set; }

    public string Status { get; set; } = "Active";

    public double ConfidenceScore { get; set; }

    public string? CancellationUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}