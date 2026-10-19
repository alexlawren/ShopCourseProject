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
        return await ApiReadHelper.GetAsync<CartDto>(_httpClient, $"/api/cart", cancellationToken);
    }

    public async Task<CartOperationResult> AddItemAsync(Guid productId, int quantity = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
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
        catch (Exception)
        {
            return new CartOperationResult(false, null, "Ошибка сети. Проверьте соединение и повторите попытку.");
        }
    }

    public async Task<CartOperationResult> UpdateItemAsync(Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PutAsJsonAsync($"/api/cart/items/{productId}", new UpdateCartItemRequest
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
        catch (Exception)
        {
            return new CartOperationResult(false, null, "Ошибка сети. Проверьте соединение и повторите попытку.");
        }
    }

    public async Task<CartOperationResult> RemoveItemAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.DeleteAsync($"/api/cart/items/{productId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new CartOperationResult(true);
            }

            var (errorMsg, errorCode) = await ParseProblemDetailsAsync(response);
            return new CartOperationResult(false, null, errorMsg, errorCode);
        }
        catch (Exception)
        {
            return new CartOperationResult(false, null, "Ошибка сети. Проверьте соединение и повторите попытку.");
        }
    }

    public async Task<CartOperationResult> ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.DeleteAsync("/api/cart", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new CartOperationResult(true);
            }

            var (errorMsg, errorCode) = await ParseProblemDetailsAsync(response);
            return new CartOperationResult(false, null, errorMsg, errorCode);
        }
        catch (Exception)
        {
            return new CartOperationResult(false, null, "Ошибка сети. Проверьте соединение и повторите попытку.");
        }
    }

    private static async Task<(string ErrorMessage, string? ErrorCode)> ParseProblemDetailsAsync(HttpResponseMessage response)
    {
        return await ApiErrorReader.ReadAsync(response);
    }
}
