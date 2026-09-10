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
    // Επιστρέφει όλες τις αποθηκευμένες συνδρομές
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
    // Επιστρέφει μία συγκεκριμένη συνδρομή
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
    // Δημιουργεί χειροκίνητα νέα συνδρομή
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
    // Παίρνει πιθανά subscription emails από Gmail
    // και τα περνάει από το Detection Engine.
    //
    // Προς το παρόν ΔΕΝ τα αποθηκεύει στη SQLite.
    // =========================================================

    [HttpPost("scan")]
    public async Task<IActionResult> ScanSubscriptions()
    {
        // Παίρνουμε το πρώτο connected Gmail account
        var account = await _context.ConnectedEmailAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync();

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
            // 1. Παίρνουμε πιθανά subscription emails
            // =================================================

            var emails =
                await _gmailService.GetSubscriptionEmailsAsync(account);

            var detectedSubscriptions =
                new List<DetectedSubscriptionResponse>();

            // =================================================
            // 2. Περνάμε κάθε email από το Detection Engine
            // =================================================

            foreach (var email in emails)
            {
                var parsedDate =
                    ParseGmailDate(email.Date);

                var candidate = new EmailCandidate
                {
                    GmailMessageId =
                        email.MessageId ?? "",

                    From =
                        email.From ?? "",

                    Subject =
                        email.Subject ?? "",

                    Snippet =
                        email.Snippet ?? "",

                    Date =
                        parsedDate
                };

                // =============================================
                // Detection Engine
                // =============================================

                var detection =
                    _detectionService.Detect(candidate);

                // Αν δεν θεωρείται subscription,
                // το αγνοούμε.
                if (!detection.IsSubscription)
                {
                    continue;
                }

                // =============================================
                // Αποτέλεσμα
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

                        Score =
                            detection.Score,

                        Confidence =
                            detection.Confidence,

                        Reasons =
                            detection.Reasons
                    });
            }

            // =============================================
            // Υψηλότερο score πρώτο
            // =============================================

            var orderedResults =
                detectedSubscriptions
                    .OrderByDescending(x => x.Score)
                    .ToList();

            return Ok(new
            {
                scannedEmails =
                    emails.Count,

                detectedSubscriptions =
                    orderedResults.Count,

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
    // Ενημερώνει μία υπάρχουσα συνδρομή
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
    // Διαγράφει μία συνδρομή
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

        _context.Subscriptions.Remove(subscription);

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // =========================================================
    // Gmail Date Parser
    //
    // Παράδειγμα Gmail:
    // Wed, 07 Jan 2026 08:12:49 +0000 (UTC)
    //
    // Το "(UTC)" ή "(EEST)" μπορεί να προκαλέσει
    // αποτυχία στο DateTimeOffset.TryParse.
    // =========================================================

    private static DateTime? ParseGmailDate(string? gmailDate)
    {
        if (string.IsNullOrWhiteSpace(gmailDate))
        {
            return null;
        }

        var cleanDate =
            gmailDate.Trim();

        // Αφαιρούμε:
        // (UTC)
        // (EEST)
        // κτλ.
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