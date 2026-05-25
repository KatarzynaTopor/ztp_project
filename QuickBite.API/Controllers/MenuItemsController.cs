using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuickBite.API.Data;
using QuickBite.API.DTOs.MenuItems;
using QuickBite.API.Models;

namespace QuickBite.API.Controllers;

[ApiController]
[Route("api/restaurants/{restaurantId}/menu")]
public class MenuItemsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MenuItemsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Lista pozycji menu danej restauracji (publiczna).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetMenu(int restaurantId, [FromQuery] bool onlyAvailable = true)
    {
        var restaurantExists = await _db.Restaurants.AnyAsync(r => r.Id == restaurantId);
        if (!restaurantExists) return NotFound("Restaurant not found.");

        var query = _db.MenuItems.Where(m => m.RestaurantId == restaurantId);
        if (onlyAvailable) query = query.Where(m => m.IsAvailable);

        var list = await query
            .OrderBy(m => m.Category).ThenBy(m => m.Name)
            .Select(m => new MenuItemDto
            {
                Id = m.Id,
                Name = m.Name,
                Description = m.Description,
                Price = m.Price,
                Category = m.Category,
                IsAvailable = m.IsAvailable,
                RestaurantId = m.RestaurantId
            })
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>Pojedyncza pozycja menu (publiczna).</summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetItem(int restaurantId, int id)
    {
        var m = await _db.MenuItems
            .FirstOrDefaultAsync(x => x.Id == id && x.RestaurantId == restaurantId);

        if (m is null) return NotFound();

        return Ok(new MenuItemDto
        {
            Id = m.Id,
            Name = m.Name,
            Description = m.Description,
            Price = m.Price,
            Category = m.Category,
            IsAvailable = m.IsAvailable,
            RestaurantId = m.RestaurantId
        });
    }

    /// <summary>Dodaj pozycję menu (tylko właściciel restauracji).</summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Restaurant))]
    public async Task<IActionResult> Create(int restaurantId, [FromBody] CreateMenuItemDto dto)
    {
        var restaurant = await _db.Restaurants.FindAsync(restaurantId);
        if (restaurant is null) return NotFound("Restaurant not found.");

        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (restaurant.OwnerId != ownerId) return Forbid();

        var item = new MenuItem
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            Category = dto.Category,
            IsAvailable = dto.IsAvailable,
            RestaurantId = restaurantId
        };

        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetItem), new { restaurantId, id = item.Id }, new { item.Id });
    }

    /// <summary>Edytuj pozycję menu (tylko właściciel restauracji).</summary>
    [HttpPut("{id}")]
    [Authorize(Roles = nameof(UserRole.Restaurant))]
    public async Task<IActionResult> Update(int restaurantId, int id, [FromBody] UpdateMenuItemDto dto)
    {
        var item = await _db.MenuItems
            .Include(m => m.Restaurant)
            .FirstOrDefaultAsync(m => m.Id == id && m.RestaurantId == restaurantId);

        if (item is null) return NotFound();

        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (item.Restaurant.OwnerId != ownerId) return Forbid();

        item.Name = dto.Name;
        item.Description = dto.Description;
        item.Price = dto.Price;
        item.Category = dto.Category;
        item.IsAvailable = dto.IsAvailable;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Usuń pozycję menu (tylko właściciel).</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = nameof(UserRole.Restaurant))]
    public async Task<IActionResult> Delete(int restaurantId, int id)
    {
        var item = await _db.MenuItems
            .Include(m => m.Restaurant)
            .FirstOrDefaultAsync(m => m.Id == id && m.RestaurantId == restaurantId);

        if (item is null) return NotFound();

        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (item.Restaurant.OwnerId != ownerId) return Forbid();

        _db.MenuItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
