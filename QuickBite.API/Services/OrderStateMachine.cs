using QuickBite.API.Models;

namespace QuickBite.API.Services;

public interface IOrderStateMachine
{
    bool CanTransition(OrderStatus from, OrderStatus to, UserRole actorRole);
    OrderStatus Transition(Order order, OrderStatus to, UserRole actorRole);
}

public class OrderStateMachine : IOrderStateMachine
{
    // (from, to) -> roles allowed to perform this transition
    private static readonly Dictionary<(OrderStatus From, OrderStatus To), UserRole[]> AllowedTransitions = new()
    {
        { (OrderStatus.Pending,        OrderStatus.Accepted),       new[] { UserRole.Restaurant } },
        { (OrderStatus.Pending,        OrderStatus.Rejected),       new[] { UserRole.Restaurant } },
        { (OrderStatus.Pending,        OrderStatus.Cancelled),      new[] { UserRole.Customer   } },

        { (OrderStatus.Accepted,       OrderStatus.InPreparation),  new[] { UserRole.Restaurant } },
        { (OrderStatus.Accepted,       OrderStatus.Cancelled),      new[] { UserRole.Customer   } },

        { (OrderStatus.InPreparation,  OrderStatus.ReadyForPickup), new[] { UserRole.Restaurant } },

        { (OrderStatus.ReadyForPickup, OrderStatus.OutForDelivery), new[] { UserRole.Courier    } },

        { (OrderStatus.OutForDelivery, OrderStatus.Delivered),      new[] { UserRole.Courier    } },
    };

    public bool CanTransition(OrderStatus from, OrderStatus to, UserRole actorRole)
    {
        if (!AllowedTransitions.TryGetValue((from, to), out var roles))
            return false;
        return roles.Contains(actorRole);
    }

    public OrderStatus Transition(Order order, OrderStatus to, UserRole actorRole)
    {
        if (!CanTransition(order.Status, to, actorRole))
            throw new InvalidOperationException(
                $"Przejście z {order.Status} do {to} jest niedozwolone dla roli {actorRole}.");

        order.Status = to;
        order.UpdatedAt = DateTime.UtcNow;
        return order.Status;
    }
}
