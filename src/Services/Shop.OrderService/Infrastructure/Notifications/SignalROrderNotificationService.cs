using Microsoft.AspNetCore.SignalR;
using Shop.OrderService.Application.Orders.Notifications;
using Shop.OrderService.Hubs;

namespace Shop.OrderService.Infrastructure.Notifications;

public sealed class SignalROrderNotificationService : IOrderNotificationService
{
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly ILogger<SignalROrderNotificationService> _logger;

    public SignalROrderNotificationService(
        IHubContext<OrderHub> hubContext,
        ILogger<SignalROrderNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyOrderCreatedAsync(
        OrderCreatedEvent evt,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userGroup = OrderHubGroups.User(userId);
            await _hubContext.Clients
                .Groups(userGroup, OrderHubGroups.Admins)
                .SendAsync(OrderHubEvents.OrderCreated, evt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send OrderCreated notification for order {OrderId}", evt.OrderId);
        }
    }

    public async Task NotifyOrderStatusChangedAsync(
        OrderStatusChangedEvent evt,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userGroup = OrderHubGroups.User(userId);
            await _hubContext.Clients
                .Groups(userGroup, OrderHubGroups.Admins)
                .SendAsync(OrderHubEvents.OrderStatusChanged, evt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send OrderStatusChanged notification for order {OrderId}", evt.OrderId);
        }
    }

    public async Task NotifyPaymentStatusChangedAsync(
        PaymentStatusChangedEvent evt,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userGroup = OrderHubGroups.User(userId);
            await _hubContext.Clients
                .Groups(userGroup, OrderHubGroups.Admins)
                .SendAsync(OrderHubEvents.PaymentStatusChanged, evt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send PaymentStatusChanged notification for order {OrderId}", evt.OrderId);
        }
    }
}
