namespace Shop.CatalogService.Application.Catalog.Dtos;

public sealed record AdminProductDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    string? ImagePath,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);
