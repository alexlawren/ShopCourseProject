namespace Shop.OrderService.Application.Orders.Notifications;

public interface IOrderNotificationService
{
    Task NotifyOrderCreatedAsync(OrderCreatedEvent evt, Guid userId, CancellationToken cancellationToken = default);

    Task NotifyOrderStatusChangedAsync(OrderStatusChangedEvent evt, Guid userId, CancellationToken cancellationToken = default);

    Task NotifyPaymentStatusChangedAsync(PaymentStatusChangedEvent evt, Guid userId, CancellationToken cancellationToken = default);
}
