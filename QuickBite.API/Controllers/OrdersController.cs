using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuickBite.API.Data;
using QuickBite.API.DTOs.Orders;
using QuickBite.API.Models;
using QuickBite.API.Services;
using QuickBite.API.Services.Pricing;

namespace QuickBite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IOrderStateMachine _stateMachine;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPricingStrategyFactory _pricingFactory;

    public OrdersController(AppDbContext db, IOrderStateMachine stateMachine,
        UserManager<ApplicationUser> userManager,
        IPricingStrategyFactory pricingFactory)
    {
        _db = db;
        _stateMachine = stateMachine;
        _userManager = userManager;
        _pricingFactory = pricingFactory;
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Customer))]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
    {
        if (dto.Items is null || dto.Items.Count == 0)
            return BadRequest(new { error = "Koszyk nie może być pusty." });

        var restaurant = await _db.Restaurants.FindAsync(dto.RestaurantId);
        if (restaurant is null)
            return BadRequest(new { error = "Restauracja nie istnieje." });
        if (!restaurant.IsActive)
            return BadRequest(new { error = "Restauracja jest nieaktywna." });

        var requestedIds = dto.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _db.MenuItems
            .Where(m => requestedIds.Contains(m.Id) && m.RestaurantId == dto.RestaurantId)
            .ToListAsync();

        if (menuItems.Count != requestedIds.Count)
        {
            var foundIds = menuItems.Select(m => m.Id).ToHashSet();
            var missing = requestedIds.Where(id => !foundIds.Contains(id)).ToList();
            return BadRequest(new
            {
                error = "Jedna lub więcej pozycji nie należy do tej restauracji albo nie istnieje.",
                missingMenuItemIds = missing
            });
        }

        var unavailable = menuItems.Where(m => !m.IsAvailable).Select(m => m.Id).ToList();
        if (unavailable.Count > 0)
            return BadRequest(new
            {
                error = "Jedna lub więcej pozycji menu jest aktualnie niedostępna.",
                unavailableMenuItemIds = unavailable
            });

        var menuLookup = menuItems.ToDictionary(m => m.Id);
        decimal itemsTotal = 0m;
        var orderItems = new List<OrderItem>();
        foreach (var req in dto.Items)
        {
            var menuItem = menuLookup[req.MenuItemId];
            var line = new OrderItem
            {
                MenuItemId = menuItem.Id,
                NameSnapshot = menuItem.Name,
                UnitPriceSnapshot = menuItem.Price,
                Quantity = req.Quantity
            };
            itemsTotal += line.LineTotal;
            orderItems.Add(line);
        }

        if (itemsTotal < restaurant.MinOrderAmount)
            return BadRequest(new
            {
                error = $"Suma zamówienia ({itemsTotal:0.00}) jest poniżej minimum dla tej restauracji ({restaurant.MinOrderAmount:0.00}).",
                minOrderAmount = restaurant.MinOrderAmount,
                currentItemsTotal = itemsTotal
            });

        IPricingStrategy strategy;
        try
        {
            strategy = _pricingFactory.Create(dto.Strategy, dto.DiscountPercent, dto.PromoCode);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var context = new PricingContext(strategy);
        var totalAmount = context.CalculateTotal(itemsTotal, restaurant.DeliveryFee);
        var discount = Math.Round(itemsTotal + restaurant.DeliveryFee - totalAmount, 2);

        var customerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var order = new Order
        {
            CustomerId = customerId,
            RestaurantEntityId = restaurant.Id,
            RestaurantId = restaurant.OwnerId,
            Status = OrderStatus.Pending,
            ItemsTotal = itemsTotal,
            DeliveryFee = restaurant.DeliveryFee,
            Discount = discount,
            TotalAmount = totalAmount,
            PricingStrategyName = context.StrategyName,
            Notes = dto.Notes,
            Items = orderItems
        };

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        var response = new OrderResponseDto
        {
            Id = order.Id,
            Status = order.Status.ToString(),
            RestaurantId = restaurant.Id,
            RestaurantName = restaurant.Name,
            ItemsTotal = order.ItemsTotal,
            DeliveryFee = order.DeliveryFee,
            Discount = order.Discount,
            TotalAmount = order.TotalAmount,
            PricingStrategy = order.PricingStrategyName ?? "Regular",
            Notes = order.Notes,
            CreatedAt = order.CreatedAt,
            Items = order.Items.Select(i => new OrderItemResponse
            {
                MenuItemId = i.MenuItemId,
                Name = i.NameSnapshot,
                UnitPrice = i.UnitPriceSnapshot,
                Quantity = i.Quantity,
                LineTotal = i.LineTotal
            }).ToList()
        };

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, response);
    }

    /// <summary>Pobierz szczegóły zamówienia.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Restaurant)
            .Include(o => o.RestaurantEntity)
            .Include(o => o.Courier)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound();

        return Ok(new
        {
            order.Id,
            Status = order.Status.ToString(),
            Customer = order.Customer.FullName,
            RestaurantName = order.RestaurantEntity?.Name ?? order.Restaurant?.FullName,
            Courier = order.Courier?.FullName,
            order.ItemsTotal,
            order.DeliveryFee,
            order.Discount,
            order.TotalAmount,
            order.PricingStrategyName,
            Items = order.Items.Select(i => new
            {
                i.MenuItemId,
                Name = i.NameSnapshot,
                UnitPrice = i.UnitPriceSnapshot,
                i.Quantity,
                i.LineTotal
            }),
            order.Notes,
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
