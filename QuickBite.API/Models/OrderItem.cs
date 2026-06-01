using System.ComponentModel.DataAnnotations;

namespace QuickBite.API.Models;

public class OrderItem
{
    public int Id { get; set; }

    [Required]
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    [Required]
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    public decimal UnitPriceSnapshot { get; set; }

    [Required, MaxLength(150)]
    public string NameSnapshot { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal LineTotal => UnitPriceSnapshot * Quantity;
}
