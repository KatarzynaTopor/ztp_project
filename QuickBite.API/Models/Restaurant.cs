using System.ComponentModel.DataAnnotations;

namespace QuickBite.API.Models;

public class Restaurant
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required, MaxLength(250)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    // Domyślna opłata za dostawę dla tej restauracji.
    public decimal DeliveryFee { get; set; }

    // Minimalna wartość zamówienia.
    public decimal MinOrderAmount { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Właściciel — użytkownik z rolą Restaurant. Jeden user może mieć kilka restauracji.
    [Required]
    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser Owner { get; set; } = null!;

    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}
