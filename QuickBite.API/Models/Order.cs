namespace QuickBite.API.Models;

public class Order
{
    public int Id { get; set; }

    public string CustomerId { get; set; } = string.Empty;
    public ApplicationUser Customer { get; set; } = null!;

    public string? RestaurantId { get; set; }
    public ApplicationUser? Restaurant { get; set; }

    public int? RestaurantEntityId { get; set; }
    public Restaurant? RestaurantEntity { get; set; }

    public string? CourierId { get; set; }
    public ApplicationUser? Courier { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal ItemsTotal { get; set; }

    public decimal DeliveryFee { get; set; }

    public decimal Discount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? PricingStrategyName { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
