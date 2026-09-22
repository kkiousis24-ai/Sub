using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;

namespace Sub.Api.Services;

public class SubscriptionEvidenceService
    : ISubscriptionEvidenceService
{
    private readonly AppDbContext _context;

    public SubscriptionEvidenceService(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> EvidenceExistsAsync(
        int connectedEmailAccountId,
        string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return false;
        }

        var normalizedMessageId =
            messageId.Trim();

        // =====================================================
        // Check evidence added during the current scan
        // but not saved yet.
        // =====================================================

        var localEvidenceExists =
            _context.SubscriptionEvidences.Local
                .Any(e =>
                {
                    if (!string.Equals(
                        e.MessageId,
                        normalizedMessageId,
                        StringComparison.Ordinal))
                    {
                        return false;
                    }

                    var subscription =
                        _context.Subscriptions.Local
                            .FirstOrDefault(s =>
                                s.Id ==
                                e.SubscriptionId);

                    return subscription != null &&
                           subscription
                               .ConnectedEmailAccountId ==
                           connectedEmailAccountId;
                });

        if (localEvidenceExists)
        {
            return true;
        }

        // =====================================================
        // Check evidence already stored in the database,
        // scoped to the connected Gmail account.
        // =====================================================

        return await
            (
                from evidence in
                    _context.SubscriptionEvidences

                join subscription in
                    _context.Subscriptions

                on evidence.SubscriptionId
                    equals subscription.Id

                where
                    subscription
                        .ConnectedEmailAccountId ==
                    connectedEmailAccountId
                    &&
                    evidence.MessageId ==
                    normalizedMessageId

                select evidence.Id
            )
            .AnyAsync();
    }
}