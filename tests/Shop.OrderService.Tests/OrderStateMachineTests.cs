using Shop.OrderService.Application.Orders.Common;
using Shop.OrderService.Domain.Enums;

namespace Shop.OrderService.Tests;

public class OrderStateMachineTests
{
    [Theory]
    [InlineData(OrderStatus.Created, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Created, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Processing)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Processing, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Processing, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Completed)]
    public void CanTransition_AllowedTransitions_ReturnsTrue(OrderStatus from, OrderStatus to)
    {
        Assert.True(OrderStateMachine.CanTransition(from, to));
    }

    [Theory]
    [InlineData(OrderStatus.Created, OrderStatus.Processing)]
    [InlineData(OrderStatus.Created, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Created, OrderStatus.Completed)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Completed)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Processing, OrderStatus.Completed)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Completed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Created)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Processing)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Completed)]
    public void CanTransition_ForbiddenTransitions_ReturnsFalse(OrderStatus from, OrderStatus to)
    {
        Assert.False(OrderStateMachine.CanTransition(from, to));
    }

    [Theory]
    [InlineData(OrderStatus.Created, true)]
    [InlineData(OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Processing, true)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Completed, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void CanCancel_CheckStatuses(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderStateMachine.CanCancel(status));
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Processing, true)]
    [InlineData(OrderStatus.Processing, OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Created, OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled, false)] // Cancelled must use cancel endpoint
    [InlineData(OrderStatus.Processing, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Completed, OrderStatus.Processing, false)]
    public void CanAdminTransition_CheckRules(OrderStatus from, OrderStatus to, bool expected)
    {
        Assert.Equal(expected, OrderStateMachine.CanAdminTransition(from, to));
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Processing)]
    [InlineData(OrderStatus.Processing, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Completed)]
    public void CanAdminTransition_WhenCancellationPending_AlwaysReturnsFalse(OrderStatus from, OrderStatus to)
    {
        Assert.False(OrderStateMachine.CanAdminTransition(from, to, CancellationState.Pending));
    }

    [Fact]
    public void CanPay_WhenCancellationPending_ReturnsFalse()
    {
        Assert.False(OrderStateMachine.CanPay(OrderStatus.Created, PaymentStatus.Pending, CancellationState.Pending));
    }

    [Theory]
    [InlineData(OrderStatus.Created, PaymentStatus.Pending, true)]
    [InlineData(OrderStatus.Cancelled, PaymentStatus.Pending, false)]
    [InlineData(OrderStatus.Created, PaymentStatus.Cancelled, false)]
    [InlineData(OrderStatus.Cancelled, PaymentStatus.Cancelled, false)]
    public void CanPay_CheckPaymentRules(OrderStatus orderStatus, PaymentStatus paymentStatus, bool expected)
    {
        Assert.Equal(expected, OrderStateMachine.CanPay(orderStatus, paymentStatus, CancellationState.None));
    }

    [Theory]
    [InlineData(OrderStatus.Processing, CancellationState.Pending, true)]
    [InlineData(OrderStatus.Confirmed, CancellationState.Pending, true)]
    [InlineData(OrderStatus.Created, CancellationState.Pending, true)]
    [InlineData(OrderStatus.Shipped, CancellationState.Pending, true)] // already pending intent allows resuming
    public void CanCancel_WhenCancellationPending_AlwaysAllowsResume(OrderStatus status, CancellationState state, bool expected)
    {
        Assert.Equal(expected, OrderStateMachine.CanCancel(status, state));
    }
}
