namespace Sub.Api.Models;

public class SubscriptionEvidence
{
    public int Id { get; set; }

    public int SubscriptionId { get; set; }

    public string MessageId { get; set; } = string.Empty;

    public string Sender { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public decimal? DetectedAmount { get; set; }

    public string? DetectedCurrency { get; set; }

    public DateTime? DetectedDate { get; set; }

    public string? Snippet { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}