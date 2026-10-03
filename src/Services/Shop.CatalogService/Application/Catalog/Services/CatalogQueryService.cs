using Microsoft.EntityFrameworkCore;
using Shop.CatalogService.Application.Catalog.Dtos;
using Shop.CatalogService.Application.Catalog.Queries;
using Shop.CatalogService.Infrastructure.Persistence;

namespace Shop.CatalogService.Application.Catalog.Services;

public sealed class CatalogQueryService : ICatalogQueryService
{
    private readonly CatalogDbContext _dbContext;

    public CatalogQueryService(CatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<ProductListItemDto>> GetProductsAsync(
        ProductQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Category.IsActive);

        // Search by Name or Description using PostgreSQL ILike
        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var searchPattern = $"%{parameters.Search.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, searchPattern) ||
                (p.Description != null && EF.Functions.ILike(p.Description, searchPattern)));
        }

        // Filter by Category
        if (parameters.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == parameters.CategoryId.Value);
        }

        // Filter by Price range
        if (parameters.MinPrice.HasValue)
        {
            query = query.Where(p => p.Price >= parameters.MinPrice.Value);
        }

        if (parameters.MaxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= parameters.MaxPrice.Value);
        }

        // Filter by Stock status:
        // null  -> no filter
        // true  -> StockQuantity > 0 (in stock)
        // false -> StockQuantity == 0 (out of stock)
        if (parameters.InStock.HasValue)
        {
            query = parameters.InStock.Value
                ? query.Where(p => p.StockQuantity > 0)
                : query.Where(p => p.StockQuantity == 0);
        }

        // Total count before pagination
        var totalItems = await query.CountAsync(cancellationToken);

        // Sorting
        var sort = string.IsNullOrWhiteSpace(parameters.Sort)
            ? ProductQueryParameters.DefaultSort
            : parameters.Sort.Trim();

        query = sort.ToLowerInvariant() switch
        {
            "priceasc" => query.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "pricedesc" => query.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            "nameasc" => query.OrderBy(p => p.Name).ThenBy(p => p.Id),
            "namedesc" => query.OrderByDescending(p => p.Name).ThenBy(p => p.Id),
            "newest" => query.OrderByDescending(p => p.CreatedAtUtc).ThenBy(p => p.Id),
            _ => query.OrderByDescending(p => p.CreatedAtUtc).ThenBy(p => p.Id)
        };

        // Pagination
        var page = parameters.Page;
        var pageSize = parameters.PageSize;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItemDto(
                p.Id,
                p.Name,
                p.Price,
                p.StockQuantity,
                p.ImagePath,
                p.CategoryId,
                p.Category.Name))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductListItemDto>(items, page, pageSize, totalItems);
    }

    public async Task<ProductDetailsDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.Id == id && p.IsActive && p.Category.IsActive)
            .Select(p => new ProductDetailsDto(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.StockQuantity,
                p.ImagePath,
                new CategoryDto(p.Category.Id, p.Category.Name, p.Category.Slug),
                p.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminCategoryDto>> GetAdminCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new AdminCategoryDto(c.Id, c.Name, c.Slug, c.IsActive, c.CreatedAtUtc, c.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<AdminProductDto>> GetAdminProductsAsync(
        AdminProductQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .AsQueryable();

        if (parameters.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == parameters.IsActive.Value);
        }

        if (parameters.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == parameters.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var searchPattern = $"%{parameters.Search.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, searchPattern) ||
                (p.Description != null && EF.Functions.ILike(p.Description, searchPattern)));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var sort = string.IsNullOrWhiteSpace(parameters.Sort)
            ? AdminProductQueryParameters.DefaultSort
            : parameters.Sort.Trim();

        query = sort.ToLowerInvariant() switch
        {
            "priceasc" => query.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "pricedesc" => query.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            "nameasc" => query.OrderBy(p => p.Name).ThenBy(p => p.Id),
            "namedesc" => query.OrderByDescending(p => p.Name).ThenBy(p => p.Id),
            "newest" => query.OrderByDescending(p => p.CreatedAtUtc).ThenBy(p => p.Id),
            _ => query.OrderByDescending(p => p.CreatedAtUtc).ThenBy(p => p.Id)
        };

        var page = parameters.Page;
        var pageSize = parameters.PageSize;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new AdminProductDto(
                p.Id,
                p.CategoryId,
                p.Category.Name,
                p.Name,
                p.Description,
                p.Price,
                p.StockQuantity,
                p.ImagePath,
                p.IsActive,
                p.CreatedAtUtc,
                p.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminProductDto>(items, page, pageSize, totalItems);
    }
}

