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
        _logger.LogInformation("OrderStatusBackgroundService uruchomiony.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessStaleOrders(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas przetwarzania zamówień w tle.");
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

        // Auto-odrzuć zamówienia Pending starsze niż PendingTimeout
        var stalePending = await db.Orders
            .Where(o => o.Status == OrderStatus.Pending && o.UpdatedAt < now - PendingTimeout)
            .ToListAsync(ct);

        foreach (var order in stalePending)
        {
            order.Status = OrderStatus.Rejected;
            order.UpdatedAt = now;
            _logger.LogInformation("Auto-odrzucono zamówienie {OrderId} (brak odpowiedzi restauracji).", order.Id);
            changed = true;
        }

        // Auto-dostarcz zamówienia OutForDelivery starsze niż DeliveryTimeout
        var staleDelivery = await db.Orders
            .Where(o => o.Status == OrderStatus.OutForDelivery && o.UpdatedAt < now - DeliveryTimeout)
            .ToListAsync(ct);

        foreach (var order in staleDelivery)
        {
            order.Status = OrderStatus.Delivered;
            order.UpdatedAt = now;
            _logger.LogInformation("Auto-dostarczono zamówienie {OrderId} (upłynął czas dostawy).", order.Id);
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }
}
