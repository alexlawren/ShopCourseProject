namespace Shop.CatalogService.Application.Catalog.Dtos;

public sealed record ProductDetailsDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    string? ImagePath,
    CategoryDto Category,
    DateTime CreatedAtUtc
);
