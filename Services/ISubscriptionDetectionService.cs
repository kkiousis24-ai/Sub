using Sub.Api.Models;

namespace Sub.Api.Services;

public interface ISubscriptionDetectionService
{
    SubscriptionDetectionResult Detect(EmailCandidate email);
}