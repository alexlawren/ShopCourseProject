using Shop.OrderService.Domain.Enums;

namespace Shop.OrderService.Domain.Entities;

public sealed class OrderStatusHistory
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime ChangedAtUtc { get; set; }

    public Order Order { get; set; } = null!;
}
