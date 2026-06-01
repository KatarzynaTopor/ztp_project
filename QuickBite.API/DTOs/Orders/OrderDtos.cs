using System.ComponentModel.DataAnnotations;
using QuickBite.API.Services.Pricing;

namespace QuickBite.API.DTOs.Orders;

public class OrderItemRequest
{
    [Required]
    public int MenuItemId { get; set; }

    [Range(1, 99)]
    public int Quantity { get; set; }
}

public class CreateOrderDto
{
    [Required]
    public int RestaurantId { get; set; }

    [Required, MinLength(1, ErrorMessage = "Koszyk nie może być pusty.")]
    public List<OrderItemRequest> Items { get; set; } = new();

    public PricingStrategyType Strategy { get; set; } = PricingStrategyType.Regular;

    [Range(0, 100)]
    public decimal? DiscountPercent { get; set; }

    public string? PromoCode { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class OrderItemResponse
{
    public int MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class OrderResponseDto
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RestaurantId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public List<OrderItemResponse> Items { get; set; } = new();

    // Breakdown finansowy
    public decimal ItemsTotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public string PricingStrategy { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
