using Microsoft.AspNetCore.Http;
using Shop.CatalogService.Application.Catalog.Commands;
using Shop.CatalogService.Application.Catalog.Dtos;

namespace Shop.CatalogService.Application.Catalog.Images;

public interface IProductImageService
{
    Task<CommandResult<ProductImageDto>> UploadImageAsync(Guid productId, IFormFile? file, CancellationToken cancellationToken);
    Task<CommandResult> RemoveImageAsync(Guid productId, CancellationToken cancellationToken);
}
