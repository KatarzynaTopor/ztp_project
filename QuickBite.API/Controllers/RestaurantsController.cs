using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuickBite.API.Data;
using QuickBite.API.DTOs.Restaurants;
using QuickBite.API.Models;

namespace QuickBite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RestaurantsController : ControllerBase
{
    private readonly AppDbContext _db;

    public RestaurantsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Lista wszystkich aktywnych restauracji (publiczne).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var query = _db.Restaurants.Include(r => r.Owner).AsQueryable();
        if (!includeInactive)
            query = query.Where(r => r.IsActive);

        var list = await query
            .OrderBy(r => r.Name)
            .Select(r => new RestaurantDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                Address = r.Address,
                PhoneNumber = r.PhoneNumber,
                DeliveryFee = r.DeliveryFee,
                MinOrderAmount = r.MinOrderAmount,
                IsActive = r.IsActive,
                OwnerId = r.OwnerId,
                OwnerName = r.Owner.FullName,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>Szczegóły restauracji (publiczne).</summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int id)
    {
        var r = await _db.Restaurants
            .Include(x => x.Owner)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (r is null) return NotFound();

        return Ok(new RestaurantDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Address = r.Address,
            PhoneNumber = r.PhoneNumber,
            DeliveryFee = r.DeliveryFee,
            MinOrderAmount = r.MinOrderAmount,
            IsActive = r.IsActive,
            OwnerId = r.OwnerId,
            OwnerName = r.Owner.FullName,
            CreatedAt = r.CreatedAt
        });
    }

    /// <summary>Restauracje zalogowanego użytkownika (rola Restaurant).</summary>
    [HttpGet("mine")]
    [Authorize(Roles = nameof(UserRole.Restaurant))]
    public async Task<IActionResult> GetMine()
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var list = await _db.Restaurants
            .Where(r => r.OwnerId == ownerId)
            .OrderBy(r => r.Name)
            .Select(r => new RestaurantDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                Address = r.Address,
                PhoneNumber = r.PhoneNumber,
                DeliveryFee = r.DeliveryFee,
                MinOrderAmount = r.MinOrderAmount,
                IsActive = r.IsActive,
                OwnerId = r.OwnerId,
                OwnerName = string.Empty,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>Utwórz nową restaurację (rola Restaurant). Właścicielem zostaje zalogowany user.</summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Restaurant))]
    public async Task<IActionResult> Create([FromBody] CreateRestaurantDto dto)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var r = new Restaurant
        {
            Name = dto.Name,
            Description = dto.Description,
            Address = dto.Address,
            PhoneNumber = dto.PhoneNumber,
            DeliveryFee = dto.DeliveryFee,
            MinOrderAmount = dto.MinOrderAmount,
            OwnerId = ownerId,
            IsActive = true
        };

        _db.Restaurants.Add(r);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = r.Id }, new { r.Id });
    }

    /// <summary>Edytuj restaurację (tylko właściciel).</summary>
    [HttpPut("{id}")]
    [Authorize(Roles = nameof(UserRole.Restaurant))]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRestaurantDto dto)
    {
        var r = await _db.Restaurants.FindAsync(id);
        if (r is null) return NotFound();

        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (r.OwnerId != ownerId) return Forbid();

        r.Name = dto.Name;
        r.Description = dto.Description;
        r.Address = dto.Address;
        r.PhoneNumber = dto.PhoneNumber;
        r.DeliveryFee = dto.DeliveryFee;
        r.MinOrderAmount = dto.MinOrderAmount;
        r.IsActive = dto.IsActive;
        r.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Usuń restaurację (tylko właściciel). Pozycje menu też zostaną usunięte (cascade).</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = nameof(UserRole.Restaurant))]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _db.Restaurants.FindAsync(id);
        if (r is null) return NotFound();

        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (r.OwnerId != ownerId) return Forbid();

        _db.Restaurants.Remove(r);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
