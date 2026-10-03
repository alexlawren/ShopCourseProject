using Shop.CatalogService.Application.Catalog.Dtos;
using Shop.CatalogService.Application.Catalog.Queries;

namespace Shop.CatalogService.Application.Catalog.Services;

public interface ICatalogQueryService
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<ProductListItemDto>> GetProductsAsync(ProductQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<ProductDetailsDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminCategoryDto>> GetAdminCategoriesAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<AdminProductDto>> GetAdminProductsAsync(AdminProductQueryParameters parameters, CancellationToken cancellationToken = default);
}

