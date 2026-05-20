using Microsoft.AspNetCore.Identity;

namespace QuickBite.API.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}

public enum UserRole
{
    Customer,
    Restaurant,
    Courier
}
