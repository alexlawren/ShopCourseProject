namespace Shop.CatalogService.Application.Catalog.Notifications;

public interface ICatalogNotificationService
{
    Task NotifyProductChangedAsync(ProductChangedEvent evt, CancellationToken cancellationToken = default);

    Task NotifyStockChangedAsync(StockChangedEvent evt, CancellationToken cancellationToken = default);
}
