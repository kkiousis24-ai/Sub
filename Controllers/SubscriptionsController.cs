using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;
using Sub.Api.Models;
using Sub.Api.Services;

namespace Sub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly GoogleGmailService _gmailService;
    private readonly ISubscriptionDetectionService _detectionService;

    public SubscriptionsController(
        AppDbContext context,
        GoogleGmailService gmailService,
        ISubscriptionDetectionService detectionService)
    {
        _context = context;
        _gmailService = gmailService;
        _detectionService = detectionService;
    }

    // =========================================================
    // GET: api/subscriptions
    // =========================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Subscription>>> GetSubscriptions()
    {
        var subscriptions = await _context.Subscriptions
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(subscriptions);
    }

    // =========================================================
    // GET: api/subscriptions/1
    // =========================================================

    [HttpGet("{id}")]
    public async Task<ActionResult<Subscription>> GetSubscription(int id)
    {
        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == id);

        if (subscription == null)
        {
            return NotFound(new
            {
                message = "Subscription not found."
            });
        }

        return Ok(subscription);
    }

    // =========================================================
    // POST: api/subscriptions
    // Manual creation
    // =========================================================

    [HttpPost]
    public async Task<ActionResult<Subscription>> CreateSubscription(
        [FromBody] Subscription subscription)
    {
        if (string.IsNullOrWhiteSpace(subscription.Merchant))
        {
            return BadRequest(new
            {
                message = "Merchant is required."
            });
        }

        if (subscription.Amount < 0)
        {
            return BadRequest(new
            {
                message = "Amount cannot be negative."
            });
        }

        subscription.Id = 0;
        subscription.CreatedAt = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(subscription.Currency))
        {
            subscription.Currency = "EUR";
        }

        if (string.IsNullOrWhiteSpace(subscription.Status))
        {
            subscription.Status = "Active";
        }

        _context.Subscriptions.Add(subscription);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetSubscription),
            new { id = subscription.Id },
            subscription
        );
    }

    // =========================================================
    // POST: api/subscriptions/scan
    //
    // Gmail
    // ↓
    // Detection Engine
    // ↓
    // Deduplication
    // ↓
    // Subscription status handling
    // ↓
    // Next Billing Date
    // ↓
    // Evidence
    // ↓
    // SQLite
    // =========================================================

    [HttpPost("scan")]
    public async Task<IActionResult> ScanSubscriptions()
    {
        var account = await _context.ConnectedEmailAccounts
            .FirstOrDefaultAsync(a =>
                a.Provider == "Google" &&
                a.IsActive);

        if (account == null)
        {
            return BadRequest(new
            {
                message = "No connected Gmail account was found."
            });
        }

        try
        {
            // =================================================
            // 1. Fetch emails
            // =================================================

            var emails =
                await _gmailService.GetSubscriptionEmailsAsync(account);

            // =================================================
            // 2. Parse + sort oldest -> newest
            // =================================================

            var candidates = emails
                .Select(email => new EmailCandidate
                {
                    GmailMessageId =
                        email.MessageId ?? string.Empty,

                    From =
                        email.From ?? string.Empty,

                    Subject =
                        email.Subject ?? string.Empty,

                    Snippet =
                        email.Snippet ?? string.Empty,

                    // =========================================
                    // Full Gmail body
                    // =========================================

                    BodyText =
                        email.BodyText ?? string.Empty,

                    Date =
                        ParseGmailDate(email.Date)
                })
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x.GmailMessageId))
                .OrderBy(x =>
                    x.Date ?? DateTime.MinValue)
                .ToList();

            var detectedSubscriptions =
                new List<DetectedSubscriptionResponse>();

            var newSubscriptions = 0;
            var updatedSubscriptions = 0;
            var canceledSubscriptions = 0;
            var newEvidenceRecords = 0;
            var skippedDuplicateEmails = 0;

            // =================================================
            // 3. Process emails
            // =================================================

            foreach (var candidate in candidates)
            {
                // =============================================
                // Email deduplication
                // =============================================

                var evidenceAlreadyExists =
                    await _context.SubscriptionEvidences
                        .AnyAsync(e =>
                            e.MessageId ==
                            candidate.GmailMessageId);

                if (evidenceAlreadyExists)
                {
                    skippedDuplicateEmails++;
                    continue;
                }

                // =============================================
                // Detection
                // =============================================

                var detection =
                    _detectionService.Detect(candidate);

                if (!detection.IsSubscription)
                {
                    continue;
                }

                var merchant =
                    string.IsNullOrWhiteSpace(
                        detection.Merchant)
                        ? "Unknown"
                        : detection.Merchant.Trim();

                var normalizedMerchant =
                    merchant.ToLowerInvariant();

                var currency =
                    string.IsNullOrWhiteSpace(
                        detection.Currency)
                        ? "EUR"
                        : detection.Currency
                            .Trim()
                            .ToUpperInvariant();

                var billingCycle =
                    string.IsNullOrWhiteSpace(
                        detection.BillingPeriod)
                        ? "Unknown"
                        : detection.BillingPeriod;

                var detectedStatus =
                    detection.SubscriptionStatus switch
                    {
                        "Canceled" => "Canceled",
                        "Active" => "Active",
                        _ => "Active"
                    };

                var confidenceScore =
                    Math.Min(
                        detection.Score / 10.0,
                        1.0
                    );

                // =============================================
                // Find existing subscription
                // =============================================

                var subscription =
                    await _context.Subscriptions
                        .FirstOrDefaultAsync(s =>
                            s.UserId ==
                            account.UserId &&

                            s.ConnectedEmailAccountId ==
                            account.Id &&

                            s.Merchant.ToLower() ==
                            normalizedMerchant);

                // =============================================
                // CREATE
                // =============================================

                if (subscription == null)
                {
                    subscription =
                        new Subscription
                        {
                            UserId =
                                account.UserId,

                            ConnectedEmailAccountId =
                                account.Id,

                            Merchant =
                                merchant,

                            PlanName =
                                null,

                            Amount =
                                detection.Amount ?? 0m,

                            Currency =
                                currency,

                            BillingCycle =
                                billingCycle,

                            // ---------------------------------
                            // Save next billing date when active.
                            // A canceled subscription should not
                            // retain a future billing date.
                            // ---------------------------------

                            NextBillingDate =
                                detectedStatus == "Canceled"
                                    ? null
                                    : detection.NextBillingDate,

                            Status =
                                detectedStatus,

                            ConfidenceScore =
                                confidenceScore,

                            CancellationUrl =
                                null,

                            CreatedAt =
                                DateTime.UtcNow
                        };

                    _context.Subscriptions.Add(
                        subscription);

                    // Χρειαζόμαστε ID για το evidence
                    await _context.SaveChangesAsync();

                    newSubscriptions++;

                    if (detectedStatus == "Canceled")
                    {
                        canceledSubscriptions++;
                    }
                }

                // =============================================
                // UPDATE EXISTING
                // =============================================

                else
                {
                    var latestEvidence =
                        await _context.SubscriptionEvidences
                            .Where(e =>
                                e.SubscriptionId ==
                                subscription.Id)
                            .OrderByDescending(e =>
                                e.DetectedDate ??
                                e.CreatedAt)
                            .FirstOrDefaultAsync();

                    var latestKnownDate =
                        latestEvidence?.DetectedDate ??
                        latestEvidence?.CreatedAt;

                    var currentEmailDate =
                        candidate.Date;

                    // Ενημέρωση status μόνο όταν το email
                    // δεν είναι παλαιότερο από το τελευταίο.
                    var canUpdateStatus =
                        latestKnownDate == null ||
                        currentEmailDate == null ||
                        currentEmailDate >= latestKnownDate;

                    if (canUpdateStatus)
                    {
                        var oldStatus =
                            subscription.Status;

                        subscription.Status =
                            detectedStatus;

                        if (
                            detectedStatus == "Canceled" &&
                            oldStatus != "Canceled")
                        {
                            canceledSubscriptions++;
                        }

                        // =====================================
                        // Next Billing Date
                        //
                        // Cancellation -> clear date
                        // Active + date detected -> save date
                        // =====================================

                        if (detectedStatus == "Canceled")
                        {
                            subscription.NextBillingDate =
                                null;
                        }
                        else if (
                            detection.NextBillingDate.HasValue)
                        {
                            subscription.NextBillingDate =
                                detection.NextBillingDate.Value;
                        }
                    }

                    // =========================================
                    // Amount
                    // =========================================

                    if (detection.Amount.HasValue &&
                        detection.Amount.Value > 0)
                    {
                        subscription.Amount =
                            detection.Amount.Value;
                    }

                    // =========================================
                    // Currency
                    // =========================================

                    if (!string.IsNullOrWhiteSpace(
                        detection.Currency))
                    {
                        subscription.Currency =
                            currency;
                    }

                    // =========================================
                    // Billing Cycle
                    // =========================================

                    if (billingCycle != "Unknown")
                    {
                        subscription.BillingCycle =
                            billingCycle;
                    }

                    // =========================================
                    // Confidence
                    // =========================================

                    if (confidenceScore >
                        subscription.ConfidenceScore)
                    {
                        subscription.ConfidenceScore =
                            confidenceScore;
                    }

                    updatedSubscriptions++;
                }

                // =============================================
                // 4. Store Evidence
                // =============================================

                var evidence =
                    new SubscriptionEvidence
                    {
                        SubscriptionId =
                            subscription.Id,

                        MessageId =
                            candidate.GmailMessageId,

                        Sender =
                            candidate.From,

                        Subject =
                            candidate.Subject,

                        DetectedAmount =
                            detection.Amount,

                        DetectedCurrency =
                            detection.Currency,

                        DetectedDate =
                            candidate.Date,

                        Snippet =
                            candidate.Snippet,

                        CreatedAt =
                            DateTime.UtcNow
                    };

                _context.SubscriptionEvidences.Add(
                    evidence);

                newEvidenceRecords++;

                // =============================================
                // Response object
                // =============================================

                detectedSubscriptions.Add(
                    new DetectedSubscriptionResponse
                    {
                        GmailMessageId =
                            candidate.GmailMessageId,

                        From =
                            candidate.From,

                        Subject =
                            candidate.Subject,

                        Date =
                            candidate.Date,

                        Merchant =
                            detection.Merchant,

                        Amount =
                            detection.Amount,

                        Currency =
                            detection.Currency,

                        BillingPeriod =
                            detection.BillingPeriod,

                        NextBillingDate =
                            detection.NextBillingDate,

                        Score =
                            detection.Score,

                        Confidence =
                            detection.Confidence,

                        Reasons =
                            detection.Reasons
                    });
            }

            // =================================================
            // 5. Update last sync
            // =================================================

            account.LastSyncAt =
                DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // =================================================
            // 6. Final response
            // =================================================

            var orderedResults =
                detectedSubscriptions
                    .OrderByDescending(x => x.Score)
                    .ToList();

            return Ok(new
            {
                message =
                    "Gmail subscription scan completed.",

                accountId =
                    account.Id,

                emailAddress =
                    account.EmailAddress,

                scannedEmails =
                    emails.Count,

                detectedSubscriptions =
                    orderedResults.Count,

                newSubscriptions =
                    newSubscriptions,

                updatedSubscriptions =
                    updatedSubscriptions,

                canceledSubscriptions =
                    canceledSubscriptions,

                newEvidenceRecords =
                    newEvidenceRecords,

                skippedDuplicateEmails =
                    skippedDuplicateEmails,

                subscriptions =
                    orderedResults
            });
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "An error occurred while scanning Gmail.",

                    error =
                        ex.Message
                });
        }
    }

    // =========================================================
    // PUT: api/subscriptions/1
    // =========================================================

    [HttpPut("{id}")]
    public async Task<ActionResult<Subscription>> UpdateSubscription(
        int id,
        [FromBody] Subscription updatedSubscription)
    {
        var subscription =
            await _context.Subscriptions.FindAsync(id);

        if (subscription == null)
        {
            return NotFound(new
            {
                message = "Subscription not found."
            });
        }

        subscription.UserId =
            updatedSubscription.UserId;

        subscription.ConnectedEmailAccountId =
            updatedSubscription.ConnectedEmailAccountId;

        subscription.Merchant =
            updatedSubscription.Merchant;

        subscription.PlanName =
            updatedSubscription.PlanName;

        subscription.Amount =
            updatedSubscription.Amount;

        subscription.Currency =
            updatedSubscription.Currency;

        subscription.BillingCycle =
            updatedSubscription.BillingCycle;

        subscription.NextBillingDate =
            updatedSubscription.NextBillingDate;

        subscription.Status =
            updatedSubscription.Status;

        subscription.ConfidenceScore =
            updatedSubscription.ConfidenceScore;

        subscription.CancellationUrl =
            updatedSubscription.CancellationUrl;

        await _context.SaveChangesAsync();

        return Ok(subscription);
    }

    // =========================================================
    // DELETE: api/subscriptions/1
    // =========================================================

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSubscription(int id)
    {
        var subscription =
            await _context.Subscriptions.FindAsync(id);

        if (subscription == null)
        {
            return NotFound(new
            {
                message = "Subscription not found."
            });
        }

        _context.Subscriptions.Remove(
            subscription);

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // =========================================================
    // Gmail Date Parser
    // =========================================================

    private static DateTime? ParseGmailDate(
        string? gmailDate)
    {
        if (string.IsNullOrWhiteSpace(gmailDate))
        {
            return null;
        }

        var cleanDate =
            gmailDate.Trim();

        var parenthesisIndex =
            cleanDate.IndexOf(" (");

        if (parenthesisIndex >= 0)
        {
            cleanDate =
                cleanDate[..parenthesisIndex];
        }

        if (DateTimeOffset.TryParse(
            cleanDate,
            out var parsedDate))
        {
            return parsedDate.UtcDateTime;
        }

        return null;
    }
}