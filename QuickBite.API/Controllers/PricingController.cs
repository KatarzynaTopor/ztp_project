using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuickBite.API.Data;
using QuickBite.API.DTOs.Pricing;
using QuickBite.API.Services.Pricing;

namespace QuickBite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PricingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPricingStrategyFactory _factory;

    public PricingController(AppDbContext db, IPricingStrategyFactory factory)
    {
        _db = db;
        _factory = factory;
    }

    /// <summary>Oblicza koszt zamówienia bez jego składania.</summary>
    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] CalculatePriceDto dto)
    {
        var restaurant = await _db.Restaurants.FindAsync(dto.RestaurantId);
        if (restaurant is null || !restaurant.IsActive)
            return BadRequest(new { error = "Restauracja nie istnieje lub jest nieaktywna." });

        var menuItemIds = dto.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _db.MenuItems
            .Where(m => menuItemIds.Contains(m.Id) && m.RestaurantId == dto.RestaurantId && m.IsAvailable)
            .ToListAsync();

        if (menuItems.Count != menuItemIds.Count)
            return BadRequest(new { error = "Jedna lub więcej pozycji menu jest niedostępna lub nie należy do tej restauracji." });

        var itemsTotal = dto.Items.Sum(req =>
            menuItems.First(m => m.Id == req.MenuItemId).Price * req.Quantity);

        IPricingStrategy strategy;
        try
        {
            strategy = _factory.Create(dto.Strategy, dto.DiscountPercent, dto.PromoCode);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var context = new PricingContext(strategy);
        var totalAmount = context.CalculateTotal(itemsTotal, restaurant.DeliveryFee);
        var discount = Math.Round(itemsTotal + restaurant.DeliveryFee - totalAmount, 2);

        return Ok(new
        {
            RestaurantName = restaurant.Name,
            ItemsTotal = itemsTotal,
            DeliveryFee = restaurant.DeliveryFee,
            Discount = discount,
            TotalAmount = totalAmount,
            StrategyUsed = context.StrategyName
        });
    }
}
