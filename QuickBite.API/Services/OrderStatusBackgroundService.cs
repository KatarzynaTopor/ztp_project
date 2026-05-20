using Microsoft.EntityFrameworkCore;
using QuickBite.API.Data;
using QuickBite.API.Models;

namespace QuickBite.API.Services;

public class OrderStatusBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PendingTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromMinutes(45);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderStatusBackgroundService> _logger;

    public OrderStatusBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<OrderStatusBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderStatusBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessStaleOrders(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing stale orders in background.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task ProcessStaleOrders(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var changed = false;

        var stalePending = await db.Orders
            .Where(o => o.Status == OrderStatus.Pending && o.UpdatedAt < now - PendingTimeout)
            .ToListAsync(ct);

        foreach (var order in stalePending)
        {
            order.Status = OrderStatus.Rejected;
            order.UpdatedAt = now;
            _logger.LogInformation("Auto-rejected stale order {OrderId} (restaurant did not respond).", order.Id);
            changed = true;
        }

        var staleDelivery = await db.Orders
            .Where(o => o.Status == OrderStatus.OutForDelivery && o.UpdatedAt < now - DeliveryTimeout)
            .ToListAsync(ct);

        foreach (var order in staleDelivery)
        {
            order.Status = OrderStatus.Delivered;
            order.UpdatedAt = now;
            _logger.LogInformation("Auto-delivered order {OrderId} (delivery timeout elapsed).", order.Id);
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }
}
