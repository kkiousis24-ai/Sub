using Sub.Api.Models;

namespace Sub.Api.Services;

public interface ISubscriptionMatchingService
{
    Task<Subscription?> FindMatchingSubscriptionAsync(
        int userId,
        int connectedEmailAccountId,
        string merchant,
        string? detectedSubscriptionName,
        string? detectedPlanName,
        string eventType);
}