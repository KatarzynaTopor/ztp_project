using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuickBite.API.Models;

namespace QuickBite.API.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Order>()
            .HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Order>()
            .HasOne(o => o.Restaurant)
            .WithMany()
            .HasForeignKey(o => o.RestaurantId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Order>()
            .HasOne(o => o.Courier)
            .WithMany()
            .HasForeignKey(o => o.CourierId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // --- Restaurant ---
        builder.Entity<Restaurant>(b =>
        {
            b.Property(r => r.DeliveryFee).HasColumnType("decimal(10,2)");
            b.Property(r => r.MinOrderAmount).HasColumnType("decimal(10,2)");

            b.HasOne(r => r.Owner)
                .WithMany()
                .HasForeignKey(r => r.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(r => r.OwnerId);
            b.HasIndex(r => r.Name);
        });

        // --- MenuItem ---
        builder.Entity<MenuItem>(b =>
        {
            b.Property(m => m.Price).HasColumnType("decimal(10,2)");

            b.HasOne(m => m.Restaurant)
                .WithMany(r => r.MenuItems)
                .HasForeignKey(m => m.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(m => m.RestaurantId);
        });
    }
}
