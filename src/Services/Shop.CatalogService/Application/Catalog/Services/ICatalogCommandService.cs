using Shop.CatalogService.Application.Catalog.Commands;
using Shop.CatalogService.Application.Catalog.Dtos;

namespace Shop.CatalogService.Application.Catalog.Services;

public interface ICatalogCommandService
{
    Task<CommandResult<CategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CommandResult> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CommandResult> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CommandResult<ProductDetailsDto>> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<CommandResult> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task<CommandResult> DeleteProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CommandResult<StockDto>> UpdateProductStockAsync(Guid id, int quantity, CancellationToken cancellationToken = default);
}
