using QuickBite.API.Models;
using QuickBite.API.Services;
using Xunit;

namespace QuickBite.Tests;

public class OrderStateMachineTests
{
    private readonly OrderStateMachine _sut = new();

    private static Order NewOrder(OrderStatus status) => new()
    {
        Id = 1,
        CustomerId = "cust-1",
        Status = status
    };

    // --- CanTransition: valid ---

    [Theory]
    [InlineData(OrderStatus.Pending,        OrderStatus.Accepted,       UserRole.Restaurant)]
    [InlineData(OrderStatus.Pending,        OrderStatus.Rejected,       UserRole.Restaurant)]
    [InlineData(OrderStatus.Pending,        OrderStatus.Cancelled,      UserRole.Customer)]
    [InlineData(OrderStatus.Accepted,       OrderStatus.InPreparation,  UserRole.Restaurant)]
    [InlineData(OrderStatus.Accepted,       OrderStatus.Cancelled,      UserRole.Customer)]
    [InlineData(OrderStatus.InPreparation,  OrderStatus.ReadyForPickup, UserRole.Restaurant)]
    [InlineData(OrderStatus.ReadyForPickup, OrderStatus.OutForDelivery, UserRole.Courier)]
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Delivered,      UserRole.Courier)]
    public void CanTransition_AllValidTransitions_ReturnsTrue(OrderStatus from, OrderStatus to, UserRole role)
    {
        Assert.True(_sut.CanTransition(from, to, role));
    }

    // --- CanTransition: wrong role ---

    [Theory]
    [InlineData(OrderStatus.Pending,        OrderStatus.Accepted,       UserRole.Customer)]
    [InlineData(OrderStatus.Pending,        OrderStatus.Accepted,       UserRole.Courier)]
    [InlineData(OrderStatus.Pending,        OrderStatus.Cancelled,      UserRole.Restaurant)]
    [InlineData(OrderStatus.Pending,        OrderStatus.Cancelled,      UserRole.Courier)]
    [InlineData(OrderStatus.ReadyForPickup, OrderStatus.OutForDelivery, UserRole.Restaurant)]
    [InlineData(OrderStatus.ReadyForPickup, OrderStatus.OutForDelivery, UserRole.Customer)]
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Delivered,      UserRole.Customer)]
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Delivered,      UserRole.Restaurant)]
    public void CanTransition_WrongRole_ReturnsFalse(OrderStatus from, OrderStatus to, UserRole role)
    {
        Assert.False(_sut.CanTransition(from, to, role));
    }

    // --- CanTransition: nonexistent transition ---

    [Theory]
    [InlineData(OrderStatus.Delivered,  OrderStatus.Pending,       UserRole.Restaurant)]
    [InlineData(OrderStatus.Rejected,   OrderStatus.Pending,       UserRole.Restaurant)]
    [InlineData(OrderStatus.Cancelled,  OrderStatus.Accepted,      UserRole.Restaurant)]
    [InlineData(OrderStatus.Pending,    OrderStatus.OutForDelivery, UserRole.Courier)]
    public void CanTransition_NonexistentTransition_ReturnsFalse(OrderStatus from, OrderStatus to, UserRole role)
    {
        Assert.False(_sut.CanTransition(from, to, role));
    }

    // --- Transition: happy path ---

    [Fact]
    public void Transition_ValidTransition_UpdatesStatus()
    {
        var order = NewOrder(OrderStatus.Pending);
        _sut.Transition(order, OrderStatus.Accepted, UserRole.Restaurant);
        Assert.Equal(OrderStatus.Accepted, order.Status);
    }

    [Fact]
    public void Transition_ValidTransition_UpdatesUpdatedAt()
    {
        var order = NewOrder(OrderStatus.Pending);
        var before = DateTime.UtcNow.AddSeconds(-1);
        _sut.Transition(order, OrderStatus.Accepted, UserRole.Restaurant);
        Assert.True(order.UpdatedAt >= before);
    }

    [Fact]
    public void Transition_FullLifecycle_ReachesDelivered()
    {
        var order = NewOrder(OrderStatus.Pending);

        _sut.Transition(order, OrderStatus.Accepted,       UserRole.Restaurant);
        _sut.Transition(order, OrderStatus.InPreparation,  UserRole.Restaurant);
        _sut.Transition(order, OrderStatus.ReadyForPickup, UserRole.Restaurant);
        _sut.Transition(order, OrderStatus.OutForDelivery, UserRole.Courier);
        _sut.Transition(order, OrderStatus.Delivered,      UserRole.Courier);

        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    // --- Transition: role mismatch → 403 (UnauthorizedAccessException) ---

    [Fact]
    public void Transition_WrongRole_ThrowsUnauthorizedAccessException()
    {
        var order = NewOrder(OrderStatus.Pending);
        Assert.Throws<UnauthorizedAccessException>(() =>
            _sut.Transition(order, OrderStatus.Accepted, UserRole.Customer));
    }

    [Fact]
    public void Transition_WrongRole_DoesNotMutateOrder()
    {
        var order = NewOrder(OrderStatus.Pending);
        try { _sut.Transition(order, OrderStatus.Accepted, UserRole.Customer); } catch { }
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    // --- Transition: nonexistent transition → 400 (InvalidOperationException) ---

    [Fact]
    public void Transition_NonexistentTransition_ThrowsInvalidOperationException()
    {
        var order = NewOrder(OrderStatus.Delivered);
        Assert.Throws<InvalidOperationException>(() =>
            _sut.Transition(order, OrderStatus.Pending, UserRole.Restaurant));
    }

    [Fact]
    public void Transition_RoleVsStateMismatch_RoleExceptionTakesPrecedence()
    {
        // Transition (Pending → Accepted) exists but Customer can't do it → UnauthorizedAccessException, not InvalidOperation
        var order = NewOrder(OrderStatus.Pending);
        Assert.Throws<UnauthorizedAccessException>(() =>
            _sut.Transition(order, OrderStatus.Accepted, UserRole.Customer));
    }

    // --- Customer cancel paths ---

    [Fact]
    public void Transition_CustomerCancelsPending_Succeeds()
    {
        var order = NewOrder(OrderStatus.Pending);
        _sut.Transition(order, OrderStatus.Cancelled, UserRole.Customer);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Transition_CustomerCancelsAccepted_Succeeds()
    {
        var order = NewOrder(OrderStatus.Accepted);
        _sut.Transition(order, OrderStatus.Cancelled, UserRole.Customer);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Transition_CustomerCancelsInPreparation_ThrowsUnauthorized()
    {
        var order = NewOrder(OrderStatus.InPreparation);
        Assert.Throws<InvalidOperationException>(() =>
            _sut.Transition(order, OrderStatus.Cancelled, UserRole.Customer));
    }
}
