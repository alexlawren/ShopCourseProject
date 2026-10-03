using Shop.OrderService.Domain.Enums;

namespace Shop.OrderService.Application.Orders.Common;

public static class OrderStateMachine
{
    private static readonly Dictionary<OrderStatus, HashSet<OrderStatus>> AllowedTransitions = new()
    {
        [OrderStatus.Created] = new HashSet<OrderStatus> { OrderStatus.Confirmed, OrderStatus.Cancelled },
        [OrderStatus.Confirmed] = new HashSet<OrderStatus> { OrderStatus.Processing, OrderStatus.Cancelled },
        [OrderStatus.Processing] = new HashSet<OrderStatus> { OrderStatus.Shipped, OrderStatus.Cancelled },
        [OrderStatus.Shipped] = new HashSet<OrderStatus> { OrderStatus.Completed },
        [OrderStatus.Completed] = new HashSet<OrderStatus>(),
        [OrderStatus.Cancelled] = new HashSet<OrderStatus>()
    };

    public static bool CanTransition(OrderStatus current, OrderStatus next)
    {
        if (AllowedTransitions.TryGetValue(current, out var nextStates))
        {
            return nextStates.Contains(next);
        }

        return false;
    }

    public static bool CanCancel(OrderStatus current, CancellationState cancellationState = CancellationState.None)
    {
        if (cancellationState == CancellationState.Pending)
        {
            return true;
        }

        return current is OrderStatus.Created or OrderStatus.Confirmed or OrderStatus.Processing;
    }

    public static bool CanAdminTransition(OrderStatus current, OrderStatus next, CancellationState cancellationState = CancellationState.None)
    {
        if (cancellationState == CancellationState.Pending)
        {
            return false;
        }

        if (next == OrderStatus.Cancelled)
        {
            return false;
        }

        return CanTransition(current, next);
    }

    public static bool CanPay(OrderStatus current, PaymentStatus paymentStatus, CancellationState cancellationState = CancellationState.None)
    {
        if (cancellationState == CancellationState.Pending)
        {
            return false;
        }

        if (current == OrderStatus.Cancelled || paymentStatus == PaymentStatus.Cancelled)
        {
            return false;
        }

        return true;
    }
}
