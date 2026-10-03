using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shop.Web.Models.Auth;
using Shop.Web.Models.Cart;

namespace Shop.Web.Services;

public record CartOperationResult(bool Succeeded, CartDto? Cart = null, string? ErrorMessage = null, string? ErrorCode = null);

public interface ICartApiClient
{
    Task<CartDto?> GetCartAsync(CancellationToken cancellationToken = default);
    Task<CartOperationResult> AddItemAsync(Guid productId, int quantity = 1, CancellationToken cancellationToken = default);
    Task<CartOperationResult> UpdateItemAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);
    Task<CartOperationResult> RemoveItemAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<CartOperationResult> ClearAsync(CancellationToken cancellationToken = default);
}

public class CartApiClient : ICartApiClient
{
    private readonly HttpClient _httpClient;

    public CartApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CartDto?> GetCartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/cart", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<CartDto>(cancellationToken: cancellationToken);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CartApiClient] GetCartAsync error: {ex.Message}");
            return null;
        }
    }

    public async Task<CartOperationResult> AddItemAsync(Guid productId, int quantity = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
            {
                ProductId = productId,
                Quantity = quantity
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var cart = await response.Content.ReadFromJsonAsync<CartDto>(cancellationToken: cancellationToken);
                return new CartOperationResult(true, cart);
            }

            var (errorMsg, errorCode) = await ParseProblemDetailsAsync(response);
            return new CartOperationResult(false, null, errorMsg, errorCode);
        }
        catch (Exception ex)
        {
            return new CartOperationResult(false, null, $"Ошибка сети: {ex.Message}");
        }
    }

    public async Task<CartOperationResult> UpdateItemAsync(Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/cart/items/{productId}", new UpdateCartItemRequest
            {
                Quantity = quantity
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var cart = await response.Content.ReadFromJsonAsync<CartDto>(cancellationToken: cancellationToken);
                return new CartOperationResult(true, cart);
            }

            var (errorMsg, errorCode) = await ParseProblemDetailsAsync(response);
            return new CartOperationResult(false, null, errorMsg, errorCode);
        }
        catch (Exception ex)
        {
            return new CartOperationResult(false, null, $"Ошибка сети: {ex.Message}");
        }
    }

    public async Task<CartOperationResult> RemoveItemAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/cart/items/{productId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new CartOperationResult(true);
            }

            var (errorMsg, errorCode) = await ParseProblemDetailsAsync(response);
            return new CartOperationResult(false, null, errorMsg, errorCode);
        }
        catch (Exception ex)
        {
            return new CartOperationResult(false, null, $"Ошибка сети: {ex.Message}");
        }
    }

    public async Task<CartOperationResult> ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync("/api/cart", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new CartOperationResult(true);
            }

            var (errorMsg, errorCode) = await ParseProblemDetailsAsync(response);
            return new CartOperationResult(false, null, errorMsg, errorCode);
        }
        catch (Exception ex)
        {
            return new CartOperationResult(false, null, $"Ошибка сети: {ex.Message}");
        }
    }

    private static async Task<(string ErrorMessage, string? ErrorCode)> ParseProblemDetailsAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
            {
                return ($"Запрос завершился с кодом {(int)response.StatusCode}", null);
            }

            var problem = JsonSerializer.Deserialize<ProblemDetailsModel>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (problem != null)
            {
                return (problem.GetDisplayMessage(), problem.GetErrorCode());
            }

            return (content, null);
        }
        catch
        {
            return ($"Ошибка HTTP {(int)response.StatusCode} ({response.ReasonPhrase})", null);
        }
    }
}
