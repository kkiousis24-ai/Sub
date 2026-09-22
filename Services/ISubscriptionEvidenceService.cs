namespace Sub.Api.Services;

public interface ISubscriptionEvidenceService
{
    Task<bool> EvidenceExistsAsync(
        int connectedEmailAccountId,
        string messageId);
}
