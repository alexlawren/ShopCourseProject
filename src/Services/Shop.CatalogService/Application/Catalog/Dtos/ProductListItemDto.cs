namespace Shop.CatalogService.Application.Catalog.Dtos;

public sealed record ProductListItemDto(
    Guid Id,
    string Name,
    decimal Price,
    int StockQuantity,
    string? ImagePath,
    Guid CategoryId,
    string CategoryName
);
