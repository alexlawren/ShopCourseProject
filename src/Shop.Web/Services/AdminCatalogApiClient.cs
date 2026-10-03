using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shop.Web.Models.Admin;
using Shop.Web.Models.Auth;
using Shop.Web.Models.Orders;

namespace Shop.Web.Services;

public record AdminOperationResult(bool Succeeded, string? ErrorMessage = null, string? ErrorCode = null)
{
    public static AdminOperationResult Success() => new(true);
    public static AdminOperationResult Failure(string message, string? code = null) => new(false, message, code);
}

public record AdminOperationResult<T>(bool Succeeded, T? Value = default, string? ErrorMessage = null, string? ErrorCode = null)
{
    public static AdminOperationResult<T> Success(T value) => new(true, value);
    public static AdminOperationResult<T> Failure(string message, string? code = null) => new(false, default, message, code);
}

public interface IAdminCatalogApiClient
{
    Task<IReadOnlyList<AdminCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<AdminOperationResult<AdminCategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<AdminProductDto>?> GetProductsAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        Guid? categoryId = null,
        bool? isActive = null,
        string? sort = null,
        CancellationToken cancellationToken = default);

    Task<AdminOperationResult<AdminProductDto>> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> DeleteProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> UpdateStockAsync(Guid id, int quantity, CancellationToken cancellationToken = default);
    Task<AdminOperationResult<string>> UploadImageAsync(Guid id, Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> DeleteImageAsync(Guid id, CancellationToken cancellationToken = default);
}

public class AdminCatalogApiClient : IAdminCatalogApiClient
{
    private readonly HttpClient _httpClient;

    public AdminCatalogApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<AdminCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/catalog/admin/categories", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<IReadOnlyList<AdminCategoryDto>>(cancellationToken: cancellationToken)
                       ?? Array.Empty<AdminCategoryDto>();
            }

            return Array.Empty<AdminCategoryDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminCatalogApiClient] GetCategoriesAsync error: {ex.Message}");
            return Array.Empty<AdminCategoryDto>();
        }
    }

    public async Task<AdminOperationResult<AdminCategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/catalog/categories", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var category = await response.Content.ReadFromJsonAsync<AdminCategoryDto>(cancellationToken: cancellationToken);
                return AdminOperationResult<AdminCategoryDto>.Success(category!);
            }

            var (error, code) = await ParseProblemDetailsAsync(response, cancellationToken);
            return AdminOperationResult<AdminCategoryDto>.Failure(error, code);
        }
        catch (Exception ex)
        {
            return AdminOperationResult<AdminCategoryDto>.Failure($"Ошибка при создании категории: {ex.Message}");
        }
    }

    public async Task<AdminOperationResult> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/catalog/categories/{id}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return AdminOperationResult.Success();
            }

            var (error, code) = await ParseProblemDetailsAsync(response, cancellationToken);
            return AdminOperationResult.Failure(error, code);
        }
        catch (Exception ex)
        {
            return AdminOperationResult.Failure($"Ошибка при обновлении категории: {ex.Message}");
        }
    }

    public async Task<AdminOperationResult> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/catalog/categories/{id}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return AdminOperationResult.Success();
            }

            var (error, code) = await ParseProblemDetailsAsync(response, cancellationToken);
            return AdminOperationResult.Failure(error, code);
        }
        catch (Exception ex)
        {
            return AdminOperationResult.Failure($"Ошибка при деактивации категории: {ex.Message}");
        }
    }

    public async Task<PagedResult<AdminProductDto>?> GetProductsAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        Guid? categoryId = null,
        bool? isActive = null,
        string? sort = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryParams = new List<string>
            {
                $"page={page}",
                $"pageSize={pageSize}"
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                queryParams.Add($"search={Uri.EscapeDataString(search.Trim())}");
            }

            if (categoryId.HasValue)
            {
                queryParams.Add($"categoryId={categoryId.Value}");
            }

            if (isActive.HasValue)
            {
                queryParams.Add($"isActive={isActive.Value.ToString().ToLowerInvariant()}");
            }

            if (!string.IsNullOrWhiteSpace(sort))
            {
                queryParams.Add($"sort={Uri.EscapeDataString(sort.Trim())}");
            }

            var url = $"/api/catalog/admin/products?{string.Join("&", queryParams)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PagedResult<AdminProductDto>>(cancellationToken: cancellationToken);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminCatalogApiClient] GetProductsAsync error: {ex.Message}");
            return null;
        }
    }

    public async Task<AdminOperationResult<AdminProductDto>> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/catalog/products", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var product = await response.Content.ReadFromJsonAsync<AdminProductDto>(cancellationToken: cancellationToken);
                return AdminOperationResult<AdminProductDto>.Success(product!);
            }

            var (error, code) = await ParseProblemDetailsAsync(response, cancellationToken);
            return AdminOperationResult<AdminProductDto>.Failure(error, code);
        }
        catch (Exception ex)
        {
            return AdminOperationResult<AdminProductDto>.Failure($"Ошибка при создании товара: {ex.Message}");
        }
    }

    public async Task<AdminOperationResult> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/catalog/products/{id}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return AdminOperationResult.Success();
            }

            var (error, code) = await ParseProblemDetailsAsync(response, cancellationToken);
            return AdminOperationResult.Failure(error, code);
        }
        catch (Exception ex)
        {
            return AdminOperationResult.Failure($"Ошибка при обновлении товара: {ex.Message}");
        }
    }

    public async Task<AdminOperationResult> DeleteProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/catalog/products/{id}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return AdminOperationResult.Success();
            }

            var (error, code) = await ParseProblemDetailsAsync(response, cancellationToken);
            return AdminOperationResult.Failure(error, code);
        }
        catch (Exception ex)
        {
            return AdminOperationResult.Failure($"Ошибка при деактивации товара: {ex.Message}");
        }
    }

    public async Task<AdminOperationResult> UpdateStockAsync(Guid id, int quantity, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PatchAsJsonAsync($"/api/catalog/products/{id}/stock", new UpdateStockRequest
            {
                Quantity = quantity
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return AdminOperationResult.Success();
            }

            var (error, code) = await ParseProblemDetailsAsync(response, cancellationToken);
            return AdminOperationResult.Failure(error, code);
        }
        catch (Exception ex)
        {
            return AdminOperationResult.Failure($"Ошибка при обновлении остатка: {ex.Message}");
        }
    }

    public async Task<AdminOperationResult<string>> UploadImageAsync(
        Guid id,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            content.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync($"/api/catalog/products/{id}/image", content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ProductImageDto>(cancellationToken: cancellationToken);
                return AdminOperationResult<string>.Success(result?.ImagePath ?? string.Empty);
            }

            var (error, code) = await ParseProblemDetailsAsync(response, cancellationToken);
            return AdminOperationResult<string>.Failure(error, code);
        }
        catch (Exception ex)
        {
            return AdminOperationResult<string>.Failure($"Ошибка при загрузке изображения: {ex.Message}");
        }
    }

    public async Task<AdminOperationResult> DeleteImageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/catalog/products/{id}/image", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return AdminOperationResult.Success();
            }

            var (error, code) = await ParseProblemDetailsAsync(response, cancellationToken);
            return AdminOperationResult.Failure(error, code);
        }
        catch (Exception ex)
        {
            return AdminOperationResult.Failure($"Ошибка при удалении изображения: {ex.Message}");
        }
    }

    private static async Task<(string ErrorMessage, string? ErrorCode)> ParseProblemDetailsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsModel>(cancellationToken: cancellationToken);
            if (problem != null)
            {
                var message = problem.GetDisplayMessage();
                var code = problem.GetErrorCode();
                return (message, code);
            }
        }
        catch
        {
            // fallback
        }

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => ("Требуется авторизация администратора.", "UNAUTHORIZED"),
            System.Net.HttpStatusCode.Forbidden => ("Доступ запрещён. Требуются права администратора.", "FORBIDDEN"),
            System.Net.HttpStatusCode.NotFound => ("Запрашиваемый ресурс не найден.", "NOT_FOUND"),
            System.Net.HttpStatusCode.Conflict => ("Конфликт при выполнении операции.", "CONFLICT"),
            _ => ($"Ошибка сервера ({(int)response.StatusCode}).", response.StatusCode.ToString())
        };
    }
}
