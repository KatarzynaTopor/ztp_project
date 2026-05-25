using System.ComponentModel.DataAnnotations;

namespace QuickBite.API.DTOs.Restaurants;

public class CreateRestaurantDto
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required, MaxLength(250)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [Range(0, 10000)]
    public decimal DeliveryFee { get; set; }

    [Range(0, 10000)]
    public decimal MinOrderAmount { get; set; }
}

public class UpdateRestaurantDto
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required, MaxLength(250)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [Range(0, 10000)]
    public decimal DeliveryFee { get; set; }

    [Range(0, 10000)]
    public decimal MinOrderAmount { get; set; }

    public bool IsActive { get; set; } = true;
}

public class RestaurantDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal MinOrderAmount { get; set; }
    public bool IsActive { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
