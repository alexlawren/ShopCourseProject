using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shop.CatalogService.Application.Catalog.Commands;
using Shop.CatalogService.Application.Catalog.Dtos;
using Shop.CatalogService.Infrastructure.Persistence;
using Shop.CatalogService.Infrastructure.Storage;

namespace Shop.CatalogService.Application.Catalog.Images;

public sealed class ProductImageService : IProductImageService
{
    private readonly CatalogDbContext _dbContext;
    private readonly IProductImageStorage _storage;
    private readonly ProductImageValidator _validator;
    private readonly ProductImageOptions _options;
    private readonly ILogger<ProductImageService> _logger;

    public ProductImageService(
        CatalogDbContext dbContext,
        IProductImageStorage storage,
        ProductImageValidator validator,
        IOptions<ProductImageOptions> options,
        ILogger<ProductImageService> logger)
    {
        _dbContext = dbContext;
        _storage = storage;
        _validator = validator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CommandResult<ProductImageDto>> UploadImageAsync(
        Guid productId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
        {
            return CommandResult<ProductImageDto>.NotFound($"Product with ID '{productId}' was not found.");
        }

        var validation = _validator.Validate(file, _options.MaxFileSizeBytes);
        if (!validation.IsValid)
        {
            return CommandResult<ProductImageDto>.BadRequest(validation.ErrorMessage!);
        }

        await using var stream = file!.OpenReadStream();
        var newImagePath = await _storage.SaveAsync(stream, validation.NormalizedExtension!, cancellationToken);
        var oldImagePath = product.ImagePath;

        product.ImagePath = newImagePath;
        product.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save product image path to database for product {ProductId}. Deleting newly stored file {NewImagePath}.", productId, newImagePath);

            try
            {
                await _storage.DeleteAsync(newImagePath, CancellationToken.None);
            }
            catch (Exception deleteEx)
            {
                _logger.LogWarning(deleteEx, "Failed to delete new file {NewImagePath} after database save error.", newImagePath);
            }

            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldImagePath))
        {
            try
            {
                await _storage.DeleteAsync(oldImagePath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old image file {OldImagePath} for product {ProductId}.", oldImagePath, productId);
            }
        }

        _logger.LogInformation("Successfully uploaded image {NewImagePath} for product {ProductId}.", newImagePath, productId);

        return CommandResult<ProductImageDto>.Success(new ProductImageDto(product.Id, newImagePath));
    }

    public async Task<CommandResult> RemoveImageAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
        {
            return CommandResult.NotFound($"Product with ID '{productId}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(product.ImagePath))
        {
            return CommandResult.Success();
        }

        var oldImagePath = product.ImagePath;
        product.ImagePath = null;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _storage.DeleteAsync(oldImagePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete physical image file {OldImagePath} during image removal for product {ProductId}.", oldImagePath, productId);
        }

        _logger.LogInformation("Successfully removed image for product {ProductId}.", productId);

        return CommandResult.Success();
    }
}
