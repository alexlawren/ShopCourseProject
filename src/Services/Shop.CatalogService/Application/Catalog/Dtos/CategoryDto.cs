namespace Shop.CatalogService.Application.Catalog.Dtos;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string Slug
);
