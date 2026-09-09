using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;
using Sub.Api.Models;

namespace Sub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        var users = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return Ok(users);
    }

    // GET: api/users/1
    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(int id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        return Ok(user);
    }

    // POST: api/users
    [HttpPost]
    public async Task<ActionResult<User>> CreateUser(
        [FromBody] User user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return BadRequest(new
            {
                message = "Email is required."
            });
        }

        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == user.Email);

        if (existingUser != null)
        {
            return Conflict(new
            {
                message = "A user with this email already exists."
            });
        }

        user.Id = 0;
        user.CreatedAt = DateTime.UtcNow;

        _context.Users.Add(user);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetUser),
            new { id = user.Id },
            user
        );
    }

    // PUT: api/users/1
    [HttpPut("{id}")]
    public async Task<ActionResult<User>> UpdateUser(
        int id,
        [FromBody] User updatedUser)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        user.Email = updatedUser.Email;
        user.DisplayName = updatedUser.DisplayName;

        await _context.SaveChangesAsync();

        return Ok(user);
    }

    // DELETE: api/users/1
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        _context.Users.Remove(user);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}