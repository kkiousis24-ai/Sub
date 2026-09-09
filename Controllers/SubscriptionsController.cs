using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;
using Sub.Api.Models;

namespace Sub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public SubscriptionsController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/subscriptions
    // Επιστρέφει όλες τις συνδρομές
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Subscription>>> GetSubscriptions()
    {
        var subscriptions = await _context.Subscriptions
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(subscriptions);
    }

    // GET: api/subscriptions/1
    // Επιστρέφει μία συγκεκριμένη συνδρομή
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

    // POST: api/subscriptions
    // Δημιουργεί νέα συνδρομή
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

    // PUT: api/subscriptions/1
    // Ενημερώνει μία υπάρχουσα συνδρομή
    [HttpPut("{id}")]
    public async Task<ActionResult<Subscription>> UpdateSubscription(
        int id,
        [FromBody] Subscription updatedSubscription)
    {
        var subscription = await _context.Subscriptions.FindAsync(id);

        if (subscription == null)
        {
            return NotFound(new
            {
                message = "Subscription not found."
            });
        }

        subscription.UserId = updatedSubscription.UserId;
        subscription.ConnectedEmailAccountId =
            updatedSubscription.ConnectedEmailAccountId;

        subscription.Merchant = updatedSubscription.Merchant;
        subscription.PlanName = updatedSubscription.PlanName;
        subscription.Amount = updatedSubscription.Amount;
        subscription.Currency = updatedSubscription.Currency;
        subscription.BillingCycle = updatedSubscription.BillingCycle;
        subscription.NextBillingDate = updatedSubscription.NextBillingDate;
        subscription.Status = updatedSubscription.Status;
        subscription.ConfidenceScore = updatedSubscription.ConfidenceScore;
        subscription.CancellationUrl = updatedSubscription.CancellationUrl;

        await _context.SaveChangesAsync();

        return Ok(subscription);
    }

    // DELETE: api/subscriptions/1
    // Διαγράφει μία συνδρομή
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSubscription(int id)
    {
        var subscription = await _context.Subscriptions.FindAsync(id);

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
}