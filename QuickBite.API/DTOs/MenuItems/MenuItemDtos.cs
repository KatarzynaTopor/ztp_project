using System.ComponentModel.DataAnnotations;

namespace QuickBite.API.DTOs.MenuItems;

public class CreateMenuItemDto
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(0.01, 100000)]
    public decimal Price { get; set; }

    [MaxLength(80)]
    public string? Category { get; set; }

    public bool IsAvailable { get; set; } = true;
}

public class UpdateMenuItemDto
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(0.01, 100000)]
    public decimal Price { get; set; }

    [MaxLength(80)]
    public string? Category { get; set; }

    public bool IsAvailable { get; set; } = true;
}

public class MenuItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? Category { get; set; }
    public bool IsAvailable { get; set; }
    public int RestaurantId { get; set; }
}
