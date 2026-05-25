using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuickBite.API.Data;
using QuickBite.API.Models;
using QuickBite.API.Services;

namespace QuickBite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IOrderStateMachine _stateMachine;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrdersController(AppDbContext db, IOrderStateMachine stateMachine,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _stateMachine = stateMachine;
        _userManager = userManager;
    }

    /// <summary>Pobierz szczegóły zamówienia.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Restaurant)
            .Include(o => o.Courier)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound();

        return Ok(new
        {
            order.Id,
            order.TotalAmount,
            Status = order.Status.ToString(),
            Customer = order.Customer.FullName,
            Restaurant = order.Restaurant?.FullName,
            Courier = order.Courier?.FullName,
            order.CreatedAt,
            order.UpdatedAt
        });
    }

    /// <summary>Zmień status zamówienia zgodnie z maszyną stanów.</summary>
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] OrderStatus newStatus)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var actor = await _userManager.FindByIdAsync(userId!);
        if (actor is null) return Unauthorized();

        var order = await _db.Orders.FindAsync(id);
        if (order is null) return NotFound();

        try
        {
            _stateMachine.Transition(order, newStatus, actor.Role);
            await _db.SaveChangesAsync();
            return Ok(new { order.Id, Status = order.Status.ToString(), order.UpdatedAt });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Zwraca dozwolone przejścia dla aktualnego stanu zamówienia i roli użytkownika.</summary>
    [HttpGet("{id}/allowed-transitions")]
    public async Task<IActionResult> GetAllowedTransitions(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var actor = await _userManager.FindByIdAsync(userId!);
        if (actor is null) return Unauthorized();

        var order = await _db.Orders.FindAsync(id);
        if (order is null) return NotFound();

        var allowed = Enum.GetValues<OrderStatus>()
            .Where(s => _stateMachine.CanTransition(order.Status, s, actor.Role))
            .Select(s => s.ToString())
            .ToList();

        return Ok(new { CurrentStatus = order.Status.ToString(), AllowedTransitions = allowed });
    }
}
