namespace Shop.CatalogService.Application.Catalog.Notifications;

public sealed record ProductChangedEvent(
    Guid ProductId,
    bool IsActive,
    DateTime UpdatedAtUtc);

public sealed record StockChangedEvent(
    Guid ProductId,
    int StockQuantity,
    DateTime UpdatedAtUtc);
