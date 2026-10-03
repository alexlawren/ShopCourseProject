using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Shop.OrderService.Application.Orders.Notifications;
using Shop.OrderService.Controllers.Common;

namespace Shop.OrderService.Hubs;

[Authorize]
public sealed class OrderHub : Hub
{
    private readonly ILogger<OrderHub> _logger;

    public OrderHub(ILogger<OrderHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        if (Context.User == null || !Context.User.TryGetUserId(out var userId))
        {
            _logger.LogWarning("Connection {ConnectionId} rejected: Missing or invalid user identity.", Context.ConnectionId);
            Context.Abort();
            return;
        }

        var userGroup = OrderHubGroups.User(userId);
        await Groups.AddToGroupAsync(Context.ConnectionId, userGroup);

        if (Context.User.IsInRole("Admin"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, OrderHubGroups.Admins);
        }

        await base.OnConnectedAsync();
    }
}
