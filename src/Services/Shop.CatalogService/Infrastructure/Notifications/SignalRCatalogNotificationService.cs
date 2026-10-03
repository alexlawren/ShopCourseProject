using Microsoft.AspNetCore.SignalR;
using Shop.CatalogService.Application.Catalog.Notifications;
using Shop.CatalogService.Hubs;

namespace Shop.CatalogService.Infrastructure.Notifications;

public sealed class SignalRCatalogNotificationService : ICatalogNotificationService
{
    private readonly IHubContext<CatalogHub> _hubContext;
    private readonly ILogger<SignalRCatalogNotificationService> _logger;

    public SignalRCatalogNotificationService(
        IHubContext<CatalogHub> hubContext,
        ILogger<SignalRCatalogNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyProductChangedAsync(
        ProductChangedEvent evt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync(
                CatalogHubEvents.ProductChanged,
                evt,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send ProductChanged notification for product {ProductId}", evt.ProductId);
        }
    }

    public async Task NotifyStockChangedAsync(
        StockChangedEvent evt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync(
                CatalogHubEvents.StockChanged,
                evt,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send StockChanged notification for product {ProductId}", evt.ProductId);
        }
    }
}
