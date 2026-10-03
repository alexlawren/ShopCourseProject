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
        try
        {
            var result = await _httpClient.GetFromJsonAsync<IReadOnlyList<CategoryDto>>("/api/catalog/categories", cancellationToken);
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<PagedResultDto<ProductListItemDto>> GetProductsAsync(CatalogQueryModel query, CancellationToken cancellationToken = default)
    {
        var url = CatalogQueryBuilder.BuildQueryString(query);
        try
        {
            var result = await _httpClient.GetFromJsonAsync<PagedResultDto<ProductListItemDto>>(url, cancellationToken);
            return result ?? new PagedResultDto<ProductListItemDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching products: {ex.Message}");
            return new PagedResultDto<ProductListItemDto>();
        }
    }

    public async Task<ProductDetailDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/catalog/products/{id}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ProductDetailDto>(cancellationToken: cancellationToken);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public string GetImageUrl(string? imagePath)
    {
        return GatewayImageUrlBuilder.BuildImageUrl(_gatewayBaseUrl, imagePath);
    }
}
