using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Shop.Web.Models.Catalog;

namespace Shop.Web.Services;

public interface ICatalogApiClient
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<PagedResultDto<ProductListItemDto>> GetProductsAsync(CatalogQueryModel query, CancellationToken cancellationToken = default);
    Task<ProductDetailDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);
    string GetImageUrl(string? imagePath);
}

public static class CatalogQueryBuilder
{
    public static string BuildQueryString(CatalogQueryModel query)
    {
        var sb = new StringBuilder("/api/catalog/products?");
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(query.Search.Trim())}");
        }

        if (query.CategoryId.HasValue && query.CategoryId.Value != Guid.Empty)
        {
            parameters.Add($"categoryId={query.CategoryId.Value}");
        }

        if (query.MinPrice.HasValue)
        {
            parameters.Add($"minPrice={query.MinPrice.Value.ToString("F2", CultureInfo.InvariantCulture)}");
        }

        if (query.MaxPrice.HasValue)
        {
            parameters.Add($"maxPrice={query.MaxPrice.Value.ToString("F2", CultureInfo.InvariantCulture)}");
        }

        if (query.InStock.HasValue)
        {
            parameters.Add($"inStock={query.InStock.Value.ToString().ToLowerInvariant()}");
        }

        if (!string.IsNullOrWhiteSpace(query.Sort))
        {
            parameters.Add($"sort={Uri.EscapeDataString(query.Sort.Trim())}");
        }

        parameters.Add($"page={Math.Max(1, query.Page)}");
        parameters.Add($"pageSize={Math.Clamp(query.PageSize, 1, 100)}");

        sb.Append(string.Join("&", parameters));
        return sb.ToString();
    }
}

public static class GatewayImageUrlBuilder
{
    public static string BuildImageUrl(string gatewayBaseUrl, string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return string.Empty;

        var baseTrimmed = gatewayBaseUrl.TrimEnd('/');
        var pathWithSlash = imagePath.StartsWith('/') ? imagePath : "/" + imagePath;
        return $"{baseTrimmed}{pathWithSlash}";
    }
}

public class CatalogApiClient : ICatalogApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayBaseUrl;

    public CatalogApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _gatewayBaseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5210";
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await ApiReadHelper.GetAsync<IReadOnlyList<CategoryDto>>(_httpClient, "/api/catalog/categories", cancellationToken) ?? [];
    }

    public async Task<PagedResultDto<ProductListItemDto>> GetProductsAsync(CatalogQueryModel query, CancellationToken cancellationToken = default)
    {
        return await ApiReadHelper.GetAsync<PagedResultDto<ProductListItemDto>>(_httpClient, CatalogQueryBuilder.BuildQueryString(query), cancellationToken) ?? new();
    }

    public async Task<ProductDetailDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await ApiReadHelper.GetAsync<ProductDetailDto>(_httpClient, $"/api/catalog/products/{id}", cancellationToken, allowNotFound: true);
    }

    public string GetImageUrl(string? imagePath)
    {
        return GatewayImageUrlBuilder.BuildImageUrl(_gatewayBaseUrl, imagePath);
    }
}
