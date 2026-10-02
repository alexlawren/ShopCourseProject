namespace Shop.CatalogService.Application.Catalog.Dtos;

public sealed record StockDto(
    Guid ProductId,
    int StockQuantity
);
