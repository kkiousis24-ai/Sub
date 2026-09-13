using Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;
using Sub.Api.Models;
using Sub.Api.Services;

namespace Sub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GmailController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly GoogleGmailService _gmailService;
    private readonly ISubscriptionDetectionService _detectionService;

    public GmailController(
        AppDbContext context,
        GoogleGmailService gmailService,
        ISubscriptionDetectionService detectionService)
    {
        _context = context;
        _gmailService = gmailService;
        _detectionService = detectionService;
    }

    // =========================================================
    // GET: api/gmail/profile/1
    // =========================================================

    [HttpGet("profile/{connectedAccountId}")]
    public async Task<IActionResult> GetProfile(
        int connectedAccountId)
    {
        var account = await _context.ConnectedEmailAccounts
            .FirstOrDefaultAsync(a =>
                a.Id == connectedAccountId &&
                a.Provider == "Google" &&
                a.IsActive);

        if (account == null)
        {
            return NotFound(new
            {
                message = "Connected Gmail account not found."
            });
        }

        try
        {
            var profile =
                await _gmailService.GetProfileAsync(account);

            account.LastSyncAt =
                DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "Gmail profile retrieved successfully.",

                accountId =
                    account.Id,

                profile
            });
        }
        catch (GoogleApiException ex)
        {
            return StatusCode(502, new
            {
                message =
                    "Google Gmail API returned an error.",

                error =
                    ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message =
                    "An unexpected error occurred.",

                error =
                    ex.Message
            });
        }
    }

    // =========================================================
    // GET: api/gmail/subscription-emails/1
    // =========================================================

    [HttpGet("subscription-emails/{connectedAccountId}")]
    public async Task<IActionResult> GetSubscriptionEmails(
        int connectedAccountId)
    {
        var account = await _context.ConnectedEmailAccounts
            .FirstOrDefaultAsync(a =>
                a.Id == connectedAccountId &&
                a.Provider == "Google" &&
                a.IsActive);

        if (account == null)
        {
            return NotFound(new
            {
                message = "Connected Gmail account not found."
            });
        }

        try
        {
            var messages =
                await _gmailService
                    .GetSubscriptionEmailsAsync(account);

            account.LastSyncAt =
                DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                accountId =
                    account.Id,

                emailAddress =
                    account.EmailAddress,

                count =
                    messages.Count,

                messages
            });
        }
        catch (GoogleApiException ex)
        {
            return StatusCode(502, new
            {
                message =
                    "Google Gmail API returned an error.",

                error =
                    ex.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message =
                    "An unexpected error occurred.",

                error =
                    ex.Message
            });
        }
    }

    // =========================================================
    // GET: api/gmail/detection-preview/1
    //
    // Gmail
    // ↓
    // Subject + Snippet + FULL BODY
    // ↓
    // Subscription Detector
    //
    // Δεν γράφει Subscription/Evidence στη βάση.
    // =========================================================

    [HttpGet("detection-preview/{connectedAccountId}")]
    public async Task<IActionResult> GetDetectionPreview(
        int connectedAccountId)
    {
        var account = await _context.ConnectedEmailAccounts
            .FirstOrDefaultAsync(a =>
                a.Id == connectedAccountId &&
                a.Provider == "Google" &&
                a.IsActive);

        if (account == null)
        {
            return NotFound(new
            {
                message = "Connected Gmail account not found."
            });
        }

        try
        {
            var messages =
                await _gmailService
                    .GetSubscriptionEmailsAsync(account);

            var detectedSubscriptions =
                new List<DetectedSubscriptionResponse>();

            foreach (var message in messages)
            {
                var candidate =
                    new EmailCandidate
                    {
                        GmailMessageId =
                            message.MessageId ??
                            string.Empty,

                        From =
                            message.From ??
                            string.Empty,

                        Subject =
                            message.Subject ??
                            string.Empty,

                        Snippet =
                            message.Snippet ??
                            string.Empty,

                        // =====================================
                        // FULL EMAIL BODY
                        // =====================================

                        BodyText =
                            message.BodyText ??
                            string.Empty,

                        Date =
                            ParseGmailDate(message.Date)
                    };

                var detection =
                    _detectionService.Detect(candidate);

                if (!detection.IsSubscription)
                {
                    continue;
                }

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

            var orderedResults =
                detectedSubscriptions
                    .OrderByDescending(x => x.Score)
                    .ToList();

            return Ok(new
            {
                message =
                    "Detection preview completed.",

                accountId =
                    account.Id,

                emailAddress =
                    account.EmailAddress,

                scannedEmails =
                    messages.Count,

                detectedSubscriptions =
                    orderedResults.Count,

                subscriptions =
                    orderedResults
            });
        }
        catch (GoogleApiException ex)
        {
            return StatusCode(502, new
            {
                message =
                    "Google Gmail API returned an error.",

                error =
                    ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message =
                    "An unexpected error occurred.",

                error =
                    ex.Message
            });
        }
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