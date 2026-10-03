using Shop.Web.Models.Orders;
using Shop.Web.Services;

namespace Shop.Web.Tests;

public class OrderEventApplierTests
{
    [Fact]
    public void ApplyStatusChanged_MatchingOrderId_UpdatesDetailsAndReturnsTrue()
    {
        var orderId = Guid.NewGuid();
        var order = new OrderDetailsDto
        {
            Id = orderId,
            Status = "Created",
            PaymentStatus = "Pending"
        };

        var changeTime = DateTime.UtcNow;
        var ev = new OrderStatusChangedEvent
        {
            OrderId = orderId,
            Status = "Confirmed",
            ChangedAtUtc = changeTime
        };

        var applied = OrderEventApplier.ApplyStatusChanged(order, ev);

        Assert.True(applied);
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal(changeTime, order.UpdatedAtUtc);
    }

    [Fact]
    public void ApplyStatusChanged_DifferentOrderId_IgnoresAndReturnsFalse()
    {
        var order = new OrderDetailsDto
        {
            Id = Guid.NewGuid(),
            Status = "Created"
        };

        var ev = new OrderStatusChangedEvent
        {
            OrderId = Guid.NewGuid(),
            Status = "Confirmed",
            ChangedAtUtc = DateTime.UtcNow
        };

        var applied = OrderEventApplier.ApplyStatusChanged(order, ev);

        Assert.False(applied);
        Assert.Equal("Created", order.Status);
    }

    [Fact]
    public void ApplyPaymentStatusChanged_MatchingOrderId_UpdatesDetailsAndReturnsTrue()
    {
        var orderId = Guid.NewGuid();
        var order = new OrderDetailsDto
        {
            Id = orderId,
            PaymentStatus = "Pending"
        };

        var changeTime = DateTime.UtcNow;
        var ev = new PaymentStatusChangedEvent
        {
            OrderId = orderId,
            PaymentStatus = "Paid",
            ChangedAtUtc = changeTime
        };

        var applied = OrderEventApplier.ApplyPaymentStatusChanged(order, ev);

        Assert.True(applied);
        Assert.Equal("Paid", order.PaymentStatus);
        Assert.Equal(changeTime, order.UpdatedAtUtc);
    }

    [Fact]
    public void ApplyStatusChanged_OrderList_UpdatesMatchingItem()
    {
        var targetId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        var list = new List<OrderListItemDto>
        {
            new() { Id = otherId, Status = "Created" },
            new() { Id = targetId, Status = "Created" }
        };

        var ev = new OrderStatusChangedEvent
        {
            OrderId = targetId,
            Status = "Processing",
            ChangedAtUtc = DateTime.UtcNow
        };

        var applied = OrderEventApplier.ApplyStatusChanged(list, ev);

        Assert.True(applied);
        Assert.Equal("Created", list[0].Status);
        Assert.Equal("Processing", list[1].Status);
    }

    [Fact]
    public void ApplyPaymentStatusChanged_OrderList_UpdatesMatchingItem()
    {
        var targetId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        var list = new List<OrderListItemDto>
        {
            new() { Id = otherId, PaymentStatus = "Pending" },
            new() { Id = targetId, PaymentStatus = "Pending" }
        };

        var ev = new PaymentStatusChangedEvent
        {
            OrderId = targetId,
            PaymentStatus = "Paid",
            ChangedAtUtc = DateTime.UtcNow
        };

        var applied = OrderEventApplier.ApplyPaymentStatusChanged(list, ev);

        Assert.True(applied);
        Assert.Equal("Pending", list[0].PaymentStatus);
        Assert.Equal("Paid", list[1].PaymentStatus);
    }

    [Fact]
    public void ApplyStatusChanged_DuplicateEvent_IsIdempotent()
    {
        var orderId = Guid.NewGuid();
        var order = new OrderDetailsDto
        {
            Id = orderId,
            Status = "Confirmed"
        };

        var ev = new OrderStatusChangedEvent
        {
            OrderId = orderId,
            Status = "Confirmed",
            ChangedAtUtc = DateTime.UtcNow
        };

        var applied = OrderEventApplier.ApplyStatusChanged(order, ev);

        Assert.True(applied);
        Assert.Equal("Confirmed", order.Status);
    }
}
