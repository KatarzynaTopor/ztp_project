using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuickBite.API.Models;

namespace QuickBite.API.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        builder.Entity<Order>(b =>
        {
            b.Property(o => o.ItemsTotal).HasColumnType("decimal(10,2)");
            b.Property(o => o.DeliveryFee).HasColumnType("decimal(10,2)");
            b.Property(o => o.Discount).HasColumnType("decimal(10,2)");
            b.Property(o => o.TotalAmount).HasColumnType("decimal(10,2)");

            b.HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(o => o.Restaurant)
                .WithMany()
                .HasForeignKey(o => o.RestaurantId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(o => o.Courier)
                .WithMany()
                .HasForeignKey(o => o.CourierId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(o => o.RestaurantEntity)
                .WithMany()
                .HasForeignKey(o => o.RestaurantEntityId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(o => o.RestaurantEntityId);
            b.HasIndex(o => o.CustomerId);
        });

        builder.Entity<OrderItem>(b =>
        {
            b.Property(oi => oi.UnitPriceSnapshot).HasColumnType("decimal(10,2)");

            b.HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(oi => oi.MenuItem)
                .WithMany()
                .HasForeignKey(oi => oi.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(oi => oi.OrderId);
            b.HasIndex(oi => oi.MenuItemId);

            b.Ignore(oi => oi.LineTotal);
        });

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
