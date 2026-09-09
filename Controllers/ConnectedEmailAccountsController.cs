using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;
using Sub.Api.Models;

namespace Sub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConnectedEmailAccountsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ConnectedEmailAccountsController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/ConnectedEmailAccounts
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConnectedEmailAccount>>> GetAccounts()
    {
        var accounts = await _context.ConnectedEmailAccounts
            .OrderByDescending(a => a.Id)
            .ToListAsync();

        return Ok(accounts);
    }

    // GET: api/ConnectedEmailAccounts/1
    [HttpGet("{id}")]
    public async Task<ActionResult<ConnectedEmailAccount>> GetAccount(int id)
    {
        var account = await _context.ConnectedEmailAccounts.FindAsync(id);

        if (account == null)
        {
            return NotFound(new
            {
                message = "Connected email account not found."
            });
        }

        return Ok(account);
    }

    // GET: api/ConnectedEmailAccounts/user/1
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<ConnectedEmailAccount>>> GetAccountsByUser(
        int userId)
    {
        var accounts = await _context.ConnectedEmailAccounts
            .Where(a => a.UserId == userId)
            .ToListAsync();

        return Ok(accounts);
    }

    // POST: api/ConnectedEmailAccounts
    [HttpPost]
    public async Task<ActionResult<ConnectedEmailAccount>> CreateAccount(
        [FromBody] ConnectedEmailAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.EmailAddress))
        {
            return BadRequest(new
            {
                message = "Email address is required."
            });
        }

        if (string.IsNullOrWhiteSpace(account.Provider))
        {
            return BadRequest(new
            {
                message = "Provider is required."
            });
        }

        var userExists = await _context.Users
            .AnyAsync(u => u.Id == account.UserId);

        if (!userExists)
        {
            return BadRequest(new
            {
                message = "User does not exist."
            });
        }

        var alreadyConnected = await _context.ConnectedEmailAccounts
            .AnyAsync(a =>
                a.UserId == account.UserId &&
                a.EmailAddress == account.EmailAddress);

        if (alreadyConnected)
        {
            return Conflict(new
            {
                message = "This email account is already connected."
            });
        }

        account.Id = 0;
        account.IsActive = true;
        account.LastSyncAt = null;

        _context.ConnectedEmailAccounts.Add(account);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetAccount),
            new { id = account.Id },
            account
        );
    }

    // DELETE: api/ConnectedEmailAccounts/1
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAccount(int id)
    {
        var account = await _context.ConnectedEmailAccounts.FindAsync(id);

        if (account == null)
        {
            return NotFound(new
            {
                message = "Connected email account not found."
            });
        }

        _context.ConnectedEmailAccounts.Remove(account);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}