using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;
using Sub.Api.Models;
using System.Security.Claims;

namespace Sub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class GoogleAuthController : ControllerBase
{
    private readonly AppDbContext _context;

    public GoogleAuthController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/auth/google?userId=1
    [HttpGet("google")]
    public IActionResult GoogleLogin([FromQuery] int userId)
    {
        var redirectUrl = Url.Action(
            nameof(GoogleCallback),
            "GoogleAuth",
            null,
            Request.Scheme
        );

        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };

        // Κρατάμε ποιος χρήστης του Sub κάνει connect το Gmail
        properties.Items["userId"] = userId.ToString();

        return Challenge(
            properties,
            GoogleDefaults.AuthenticationScheme
        );
    }

    // GET: api/auth/google/callback
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        var authResult = await HttpContext.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        if (!authResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Google authentication failed."
            });
        }

        var email = authResult.Principal?
            .FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new
            {
                message = "Google account email was not returned."
            });
        }

        if (!authResult.Properties!.Items.TryGetValue(
                "userId",
                out var userIdString) ||
            !int.TryParse(userIdString, out var userId))
        {
            return BadRequest(new
            {
                message = "Sub user ID was not found."
            });
        }

        var userExists = await _context.Users
            .AnyAsync(u => u.Id == userId);

        if (!userExists)
        {
            return BadRequest(new
            {
                message = "Sub user does not exist."
            });
        }

        var accessToken = authResult.Properties
            .GetTokenValue("access_token");

        var refreshToken = authResult.Properties
            .GetTokenValue("refresh_token");

        var expiresAtString = authResult.Properties
            .GetTokenValue("expires_at");

        DateTime? expiresAt = null;

        if (DateTime.TryParse(expiresAtString, out var parsedDate))
        {
            expiresAt = parsedDate.ToUniversalTime();
        }

        var existingAccount =
            await _context.ConnectedEmailAccounts
                .FirstOrDefaultAsync(a =>
                    a.UserId == userId &&
                    a.EmailAddress == email &&
                    a.Provider == "Google");

        if (existingAccount == null)
        {
            existingAccount = new ConnectedEmailAccount
            {
                UserId = userId,
                Provider = "Google",
                EmailAddress = email,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenExpiresAt = expiresAt,
                LastSyncAt = null,
                IsActive = true
            };

            _context.ConnectedEmailAccounts.Add(existingAccount);
        }
        else
        {
            existingAccount.AccessToken = accessToken;

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                existingAccount.RefreshToken = refreshToken;
            }

            existingAccount.TokenExpiresAt = expiresAt;
            existingAccount.IsActive = true;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Gmail connected successfully.",
            account = new
            {
                existingAccount.Id,
                existingAccount.EmailAddress,
                existingAccount.Provider,
                existingAccount.IsActive
            }
        });
    }
}