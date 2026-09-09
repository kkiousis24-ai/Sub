namespace Sub.Api.Models;

public class CancellationRequest
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int SubscriptionId { get; set; }

    public string Method { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public string? CancellationUrl { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}