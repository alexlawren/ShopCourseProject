using Microsoft.EntityFrameworkCore;
using Shop.CatalogService.Application.Catalog.Commands;
using Shop.CatalogService.Application.Catalog.Dtos;
using Shop.CatalogService.Domain.Entities;
using Shop.CatalogService.Infrastructure.Persistence;

namespace Shop.CatalogService.Application.Catalog.Services;

public sealed class CatalogCommandService : ICatalogCommandService
{
    private readonly CatalogDbContext _dbContext;

    public CatalogCommandService(CatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CommandResult<CategoryDto>> CreateCategoryAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = request.Slug.Trim().ToLowerInvariant();
        var normalizedName = request.Name.Trim();

        var slugExists = await _dbContext.Categories
            .AnyAsync(c => c.Slug == normalizedSlug, cancellationToken);

        if (slugExists)
        {
            return CommandResult<CategoryDto>.Conflict(
                $"A category with slug '{normalizedSlug}' already exists.");
        }

        var utcNow = DateTime.UtcNow;
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Slug = normalizedSlug,
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CommandResult<CategoryDto>.Success(
            new CategoryDto(category.Id, category.Name, category.Slug));
    }

    public async Task<CommandResult> UpdateCategoryAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
        {
            return CommandResult.NotFound($"Category with ID '{id}' was not found.");
        }

        var normalizedSlug = request.Slug.Trim().ToLowerInvariant();
        var normalizedName = request.Name.Trim();

        var slugExists = await _dbContext.Categories
            .AnyAsync(c => c.Slug == normalizedSlug && c.Id != id, cancellationToken);

        if (slugExists)
        {
            return CommandResult.Conflict(
                $"A category with slug '{normalizedSlug}' already exists.");
        }

        category.Name = normalizedName;
        category.Slug = normalizedSlug;
        category.IsActive = request.IsActive;
        category.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return CommandResult.Success();
    }

    public async Task<CommandResult> DeleteCategoryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
        {
            return CommandResult.NotFound($"Category with ID '{id}' was not found.");
        }

        if (!category.IsActive)
        {
            // Idempotent: already soft-deleted
            return CommandResult.Success();
        }

        category.IsActive = false;
        category.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return CommandResult.Success();
    }

    public async Task<CommandResult<ProductDetailsDto>> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CategoryId == Guid.Empty)
        {
            return CommandResult<ProductDetailsDto>.BadRequest("CategoryId cannot be empty.");
        }

        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category is null)
        {
            return CommandResult<ProductDetailsDto>.BadRequest(
                $"Category with ID '{request.CategoryId}' does not exist.");
        }

        if (!category.IsActive)
        {
            return CommandResult<ProductDetailsDto>.BadRequest(
                $"Cannot create product in inactive category '{category.Name}'.");
        }

        var utcNow = DateTime.UtcNow;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            ImagePath = null,
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var details = new ProductDetailsDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.StockQuantity,
            product.ImagePath,
            new CategoryDto(category.Id, category.Name, category.Slug),
            product.CreatedAtUtc);

        return CommandResult<ProductDetailsDto>.Success(details);
    }

    public async Task<CommandResult> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CategoryId == Guid.Empty)
        {
            return CommandResult.BadRequest("CategoryId cannot be empty.");
        }

        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return CommandResult.NotFound($"Product with ID '{id}' was not found.");
        }

        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category is null)
        {
            return CommandResult.BadRequest(
                $"Category with ID '{request.CategoryId}' does not exist.");
        }

        if (!category.IsActive)
        {
            return CommandResult.BadRequest(
                $"Cannot assign product to inactive category '{category.Name}'.");
        }

        product.CategoryId = category.Id;
        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Price = request.Price;
        product.IsActive = request.IsActive;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return CommandResult.Success();
    }

    public async Task<CommandResult> DeleteProductAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return CommandResult.NotFound($"Product with ID '{id}' was not found.");
        }

        if (!product.IsActive)
        {
            // Idempotent: already soft-deleted
            return CommandResult.Success();
        }

        product.IsActive = false;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return CommandResult.Success();
    }

    public async Task<CommandResult<StockDto>> UpdateProductStockAsync(
        Guid id,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity < 0)
        {
            return CommandResult<StockDto>.BadRequest("StockQuantity must be greater than or equal to 0.");
        }

        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return CommandResult<StockDto>.NotFound($"Product with ID '{id}' was not found.");
        }

        product.StockQuantity = quantity;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return CommandResult<StockDto>.Success(new StockDto(product.Id, product.StockQuantity));
    }
}
