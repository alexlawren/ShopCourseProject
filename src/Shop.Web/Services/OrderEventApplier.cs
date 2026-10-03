using Shop.Web.Models.Orders;

namespace Shop.Web.Services;

public static class OrderEventApplier
{
    public static bool ApplyStatusChanged(OrderDetailsDto? currentOrder, OrderStatusChangedEvent? ev)
    {
        if (currentOrder == null || ev == null || currentOrder.Id != ev.OrderId)
            return false;

        currentOrder.Status = ev.Status;
        currentOrder.UpdatedAtUtc = ev.ChangedAtUtc;
        return true;
    }

    public static bool ApplyPaymentStatusChanged(OrderDetailsDto? currentOrder, PaymentStatusChangedEvent? ev)
    {
        if (currentOrder == null || ev == null || currentOrder.Id != ev.OrderId)
            return false;

        currentOrder.PaymentStatus = ev.PaymentStatus;
        currentOrder.UpdatedAtUtc = ev.ChangedAtUtc;
        return true;
    }

    public static bool ApplyStatusChanged(IList<OrderListItemDto>? orders, OrderStatusChangedEvent? ev)
    {
        if (orders == null || ev == null)
            return false;

        var target = orders.FirstOrDefault(o => o.Id == ev.OrderId);
        if (target == null)
            return false;

        target.Status = ev.Status;
        return true;
    }

    public static bool ApplyPaymentStatusChanged(IList<OrderListItemDto>? orders, PaymentStatusChangedEvent? ev)
    {
        if (orders == null || ev == null)
            return false;

        var target = orders.FirstOrDefault(o => o.Id == ev.OrderId);
        if (target == null)
            return false;

        target.PaymentStatus = ev.PaymentStatus;
        return true;
    }
}
