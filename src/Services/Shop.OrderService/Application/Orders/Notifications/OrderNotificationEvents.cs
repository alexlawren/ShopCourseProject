namespace Shop.OrderService.Application.Orders.Notifications;

public sealed record OrderCreatedEvent(
    Guid OrderId,
    string Status,
    string PaymentStatus,
    decimal TotalAmount,
    DateTime CreatedAtUtc);

public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    string Status,
    DateTime ChangedAtUtc);

public sealed record PaymentStatusChangedEvent(
    Guid OrderId,
    string PaymentStatus,
    DateTime ChangedAtUtc);
