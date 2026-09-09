using Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;
using Sub.Api.Services;

namespace Sub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GmailController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly GoogleGmailService _gmailService;

    public GmailController(
        AppDbContext context,
        GoogleGmailService gmailService)
    {
        _context = context;
        _gmailService = gmailService;
    }

    // GET: api/gmail/profile/1
    [HttpGet("profile/{connectedAccountId}")]
    public async Task<IActionResult> GetProfile(int connectedAccountId)
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
            var profile = await _gmailService.GetProfileAsync(account);

            account.LastSyncAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Gmail profile retrieved successfully.",
                accountId = account.Id,
                profile
            });
        }
        catch (GoogleApiException ex)
        {
            return StatusCode(502, new
            {
                message = "Google Gmail API returned an error.",
                error = ex.Message
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
                message = "An unexpected error occurred.",
                error = ex.Message
            });
        }
    }
    // GET: api/gmail/subscription-emails/1
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
            await _gmailService.GetSubscriptionEmailsAsync(account);

        account.LastSyncAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            accountId = account.Id,
            emailAddress = account.EmailAddress,
            count = messages.Count,
            messages
        });
    }
    catch (Google.GoogleApiException ex)
    {
        return StatusCode(502, new
        {
            message = "Google Gmail API returned an error.",
            error = ex.Message
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new
        {
            message = "An unexpected error occurred.",
            error = ex.Message
        });
    }
}
}