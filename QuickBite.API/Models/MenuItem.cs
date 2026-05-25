using System.ComponentModel.DataAnnotations;

namespace QuickBite.API.Models;

public class MenuItem
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public decimal Price { get; set; }

    [MaxLength(80)]
    public string? Category { get; set; }

    public bool IsAvailable { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public int RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;
}
