namespace Sub.Api.Models;

public class DetectedSubscriptionResponse
{
    public string GmailMessageId { get; set; } = "";

    public string From { get; set; } = "";

    public string Subject { get; set; } = "";

    public DateTime? Date { get; set; }

    public string? Merchant { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }

    public string? BillingPeriod { get; set; }

    public int Score { get; set; }

    public string Confidence { get; set; } = "";

    public List<string> Reasons { get; set; } = new();
}