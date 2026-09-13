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
    public async Task<ActionResult<IEnumerable<Subscription>>>
        GetSubscriptions()
    {
        var subscriptions =
            await _context.Subscriptions
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

        return Ok(subscriptions);
    }

    // =========================================================
    // GET: api/subscriptions/1
    // =========================================================

    [HttpGet("{id}")]
    public async Task<ActionResult<Subscription>>
        GetSubscription(int id)
    {
        var subscription =
            await _context.Subscriptions
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
    public async Task<ActionResult<Subscription>>
        CreateSubscription(
            [FromBody] Subscription subscription)
    {
        if (string.IsNullOrWhiteSpace(
            subscription.Merchant))
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

        if (string.IsNullOrWhiteSpace(
            subscription.Currency))
        {
            subscription.Currency = "EUR";
        }

        if (string.IsNullOrWhiteSpace(
            subscription.Status))
        {
            subscription.Status = "Active";
        }

        _context.Subscriptions.Add(subscription);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetSubscription),
            new
            {
                id = subscription.Id
            },
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
    // Email Deduplication
    // ↓
    // Safe Subscription Grouping
    // ↓
    // Status Handling
    // ↓
    // Evidence
    // ↓
    // SQLite
    // =========================================================

    [HttpPost("scan")]
    public async Task<IActionResult>
        ScanSubscriptions()
    {
        var account =
            await _context.ConnectedEmailAccounts
                .FirstOrDefaultAsync(a =>
                    a.Provider == "Google" &&
                    a.IsActive);

        if (account == null)
        {
            return BadRequest(new
            {
                message =
                    "No connected Gmail account was found."
            });
        }

        try
        {
            // =================================================
            // 1. Fetch emails
            // =================================================

            var emails =
                await _gmailService
                    .GetSubscriptionEmailsAsync(account);

            // =================================================
            // 2. Parse + sort oldest -> newest
            //
            // This is important because:
            //
            // Cancellation -> Activation -> Renewal
            //
            // should be processed chronologically.
            // =================================================

            var candidates =
                emails
                    .Select(email =>
                        new EmailCandidate
                        {
                            GmailMessageId =
                                email.MessageId ??
                                string.Empty,

                            From =
                                email.From ??
                                string.Empty,

                            Subject =
                                email.Subject ??
                                string.Empty,

                            Snippet =
                                email.Snippet ??
                                string.Empty,

                            BodyText =
                                email.BodyText ??
                                string.Empty,

                            Date =
                                ParseGmailDate(
                                    email.Date)
                        })
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(
                            x.GmailMessageId))
                    .OrderBy(x =>
                        x.Date ??
                        DateTime.MinValue)
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

                var detectedPlanName =
                    string.IsNullOrWhiteSpace(
                        detection.PlanName)
                        ? null
                        : detection.PlanName.Trim();

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
                        : detection.BillingPeriod.Trim();

                // =============================================
                // Status
                //
                // EventType has priority because it represents
                // the meaning of this specific email.
                // =============================================

                var detectedStatus =
                    detection.EventType switch
                    {
                        "Cancellation" =>
                            "Canceled",

                        "Activation" =>
                            "Active",

                        "Renewal" =>
                            "Active",

                        "Trial" =>
                            "Active",

                        _ =>
                            detection.SubscriptionStatus switch
                            {
                                "Canceled" =>
                                    "Canceled",

                                "Active" =>
                                    "Active",

                                _ =>
                                    "Active"
                            }
                    };

                var confidenceScore =
                    Math.Min(
                        detection.Score / 10.0,
                        1.0
                    );

                // =============================================
                // Find existing subscription safely
                // =============================================

                var subscription =
                    await FindMatchingSubscriptionAsync(
                        account.UserId,
                        account.Id,
                        merchant,
                        detectedPlanName,
                        detection.EventType);

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
                                detectedPlanName,

                            Amount =
                                detection.Amount ??
                                0m,

                            Currency =
                                currency,

                            BillingCycle =
                                billingCycle,

                            NextBillingDate =
                                detectedStatus ==
                                "Canceled"
                                    ? null
                                    : detection
                                        .NextBillingDate,

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

                    // Χρειαζόμαστε το ID
                    // πριν δημιουργήσουμε evidence.
                    await _context.SaveChangesAsync();

                    newSubscriptions++;

                    if (detectedStatus ==
                        "Canceled")
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
                        await _context
                            .SubscriptionEvidences
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

                    // -----------------------------------------
                    // Do not allow an older email to overwrite
                    // the latest known status.
                    // -----------------------------------------

                    var canUpdateStatus =
                        latestKnownDate == null ||
                        currentEmailDate == null ||
                        currentEmailDate >=
                        latestKnownDate;

                    if (canUpdateStatus)
                    {
                        var oldStatus =
                            subscription.Status;

                        subscription.Status =
                            detectedStatus;

                        if (
                            detectedStatus ==
                            "Canceled" &&
                            oldStatus !=
                            "Canceled")
                        {
                            canceledSubscriptions++;
                        }

                        // =====================================
                        // Next Billing Date
                        // =====================================

                        if (detectedStatus ==
                            "Canceled")
                        {
                            subscription
                                .NextBillingDate =
                                null;
                        }
                        else if (
                            detection
                                .NextBillingDate
                                .HasValue)
                        {
                            subscription
                                .NextBillingDate =
                                detection
                                    .NextBillingDate
                                    .Value;
                        }
                    }

                    // =========================================
                    // Plan Name
                    //
                    // We only fill an empty PlanName here.
                    //
                    // We never replace a specific Twitch
                    // channel such as "lagmasterpiece"
                    // with a generic "Tier 1..." value.
                    // =========================================

                    if (
                        string.IsNullOrWhiteSpace(
                            subscription.PlanName) &&
                        !string.IsNullOrWhiteSpace(
                            detectedPlanName))
                    {
                        subscription.PlanName =
                            detectedPlanName;
                    }

                    // =========================================
                    // Amount
                    // =========================================

                    if (
                        detection.Amount.HasValue &&
                        detection.Amount.Value > 0)
                    {
                        subscription.Amount =
                            detection.Amount.Value;
                    }

                    // =========================================
                    // Currency
                    // =========================================

                    if (
                        !string.IsNullOrWhiteSpace(
                            detection.Currency))
                    {
                        subscription.Currency =
                            currency;
                    }

                    // =========================================
                    // Billing Cycle
                    // =========================================

                    if (billingCycle !=
                        "Unknown")
                    {
                        subscription.BillingCycle =
                            billingCycle;
                    }

                    // =========================================
                    // Confidence
                    // =========================================

                    if (
                        confidenceScore >
                        subscription.ConfidenceScore)
                    {
                        subscription
                            .ConfidenceScore =
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

                        PlanName =
                            detection.PlanName,

                        EventType =
                            detection.EventType,

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
                    .OrderByDescending(x =>
                        x.Score)
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
                StatusCodes
                    .Status500InternalServerError,
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
    // SAFE SUBSCRIPTION MATCHING
    // =========================================================

    private async Task<Subscription?>
        FindMatchingSubscriptionAsync(
            int userId,
            int connectedEmailAccountId,
            string merchant,
            string? detectedPlanName,
            string eventType)
    {
        var normalizedMerchant =
            merchant
                .Trim()
                .ToLowerInvariant();

        var merchantSubscriptions =
            await _context.Subscriptions
                .Where(s =>
                    s.UserId ==
                    userId &&

                    s.ConnectedEmailAccountId ==
                    connectedEmailAccountId &&

                    s.Merchant.ToLower() ==
                    normalizedMerchant)
                .ToListAsync();

        if (merchantSubscriptions.Count == 0)
        {
            return null;
        }

        // =====================================================
        // TWITCH
        //
        // Twitch is special because:
        //
        // "lagmasterpiece"
        //      = real channel identity
        //
        // "Tier 1 - 1 Month Subscription - GR"
        //      = generic product/plan
        //
        // The Tier text does NOT identify the channel.
        // =====================================================

        if (merchant.Equals(
            "Twitch",
            StringComparison.OrdinalIgnoreCase))
        {
            // -------------------------------------------------
            // Specific Twitch channel
            // -------------------------------------------------

            if (
                !string.IsNullOrWhiteSpace(
                    detectedPlanName) &&
                !IsGenericTwitchPlanName(
                    detectedPlanName))
            {
                return merchantSubscriptions
                    .FirstOrDefault(s =>
                        PlanNamesEqual(
                            s.PlanName,
                            detectedPlanName));
            }

            // -------------------------------------------------
            // Generic Twitch Tier plan
            // -------------------------------------------------

            if (
                !string.IsNullOrWhiteSpace(
                    detectedPlanName) &&
                IsGenericTwitchPlanName(
                    detectedPlanName))
            {
                var genericMatches =
                    merchantSubscriptions
                        .Where(s =>
                            PlanNamesEqual(
                                s.PlanName,
                                detectedPlanName))
                        .ToList();

                // Activation represents a new purchase.
                //
                // Do not automatically merge it with an older
                // channel simply because the merchant is Twitch.
                if (eventType.Equals(
                    "Activation",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                // Renewal / Cancellation can update an existing
                // generic subscription only when there is
                // exactly one possible generic match.
                if (genericMatches.Count == 1)
                {
                    return genericMatches[0];
                }

                // Multiple generic matches means ambiguity.
                // Do not guess.
                return null;
            }

            // -------------------------------------------------
            // Twitch email without any usable PlanName
            // -------------------------------------------------

            return null;
        }

        // =====================================================
        // OTHER MERCHANTS
        // =====================================================

        if (!string.IsNullOrWhiteSpace(
            detectedPlanName))
        {
            var exactPlanMatch =
                merchantSubscriptions
                    .FirstOrDefault(s =>
                        PlanNamesEqual(
                            s.PlanName,
                            detectedPlanName));

            if (exactPlanMatch != null)
            {
                return exactPlanMatch;
            }
        }

        // For other merchants, merchant-only matching is used
        // only when there is exactly one possible subscription.
        if (merchantSubscriptions.Count == 1)
        {
            return merchantSubscriptions[0];
        }

        return null;
    }

    // =========================================================
    // Twitch generic plan detector
    // =========================================================

    private static bool
        IsGenericTwitchPlanName(
            string? planName)
    {
        if (string.IsNullOrWhiteSpace(
            planName))
        {
            return false;
        }

        return
            planName.StartsWith(
                "Tier ",
                StringComparison.OrdinalIgnoreCase) &&

            planName.Contains(
                "Subscription",
                StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================
    // Plan comparison
    // =========================================================

    private static bool
        PlanNamesEqual(
            string? first,
            string? second)
    {
        if (
            string.IsNullOrWhiteSpace(first) ||
            string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        return first.Trim().Equals(
            second.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================
    // PUT: api/subscriptions/1
    // =========================================================

    [HttpPut("{id}")]
    public async Task<ActionResult<Subscription>>
        UpdateSubscription(
            int id,
            [FromBody]
            Subscription updatedSubscription)
    {
        var subscription =
            await _context.Subscriptions
                .FindAsync(id);

        if (subscription == null)
        {
            return NotFound(new
            {
                message =
                    "Subscription not found."
            });
        }

        subscription.UserId =
            updatedSubscription.UserId;

        subscription.ConnectedEmailAccountId =
            updatedSubscription
                .ConnectedEmailAccountId;

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
            updatedSubscription
                .ConfidenceScore;

        subscription.CancellationUrl =
            updatedSubscription
                .CancellationUrl;

        await _context.SaveChangesAsync();

        return Ok(subscription);
    }

    // =========================================================
    // DELETE: api/subscriptions/1
    // =========================================================

    [HttpDelete("{id}")]
    public async Task<IActionResult>
        DeleteSubscription(int id)
    {
        var subscription =
            await _context.Subscriptions
                .FindAsync(id);

        if (subscription == null)
        {
            return NotFound(new
            {
                message =
                    "Subscription not found."
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

    private static DateTime?
        ParseGmailDate(
            string? gmailDate)
    {
        if (string.IsNullOrWhiteSpace(
            gmailDate))
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