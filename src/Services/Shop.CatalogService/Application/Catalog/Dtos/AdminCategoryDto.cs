namespace Shop.CatalogService.Application.Catalog.Dtos;

public sealed record AdminCategoryDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);
