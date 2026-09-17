using Sub.Api.Models;

namespace Sub.Api.Services;

public interface ISubscriptionLifecycleService
{
    bool ApplyDetection(
        Subscription subscription,
        SubscriptionDetectionResult detection,
        bool canUpdateStatus);
}