using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shop.Web.Models.Auth;
using Shop.Web.Models.Orders;

namespace Shop.Web.Services;

public record OrderOperationResult<T>(bool Succeeded, T? Value = default, int StatusCode = 0, string? ErrorMessage = null, string? ErrorCode = null);

public interface IOrderApiClient
{
    Task<OrderOperationResult<OrderDetailsDto>> CheckoutAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderListItemDto>?> GetOrdersAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<OrderDetailsDto?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<OrderOperationResult<OrderDetailsDto>> PayAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<OrderOperationResult<OrderDetailsDto>> CancelAsync(Guid orderId, CancellationToken cancellationToken = default);
}

public class OrderApiClient : IOrderApiClient
{
    private readonly HttpClient _httpClient;

    public OrderApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OrderOperationResult<OrderDetailsDto>> CheckoutAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/orders", new CheckoutRequest
            {
                RequestId = requestId
            }, cancellationToken);

            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                var order = await response.Content.ReadFromJsonAsync<OrderDetailsDto>(cancellationToken: cancellationToken);
                return new OrderOperationResult<OrderDetailsDto>(true, order, statusCode);
            }

            var (errorMsg, errorCode) = await ParseProblemDetailsAsync(response);
            return new OrderOperationResult<OrderDetailsDto>(false, null, statusCode, errorMsg, errorCode);
        }
        catch (Exception)
        {
            return new OrderOperationResult<OrderDetailsDto>(false, null, 0, "Ошибка сети. Проверьте соединение и повторите попытку.");
        }
    }

    public async Task<PagedResult<OrderListItemDto>?> GetOrdersAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return await ApiReadHelper.GetAsync<PagedResult<OrderListItemDto>>(_httpClient, $"/api/orders?page={page}&pageSize={pageSize}", cancellationToken);
    }

    public async Task<OrderDetailsDto?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await ApiReadHelper.GetAsync<OrderDetailsDto>(_httpClient, $"/api/orders/{orderId}", cancellationToken, allowNotFound: true);
    }

    public async Task<OrderOperationResult<OrderDetailsDto>> PayAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsync($"/api/orders/{orderId}/pay", null, cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                var order = await response.Content.ReadFromJsonAsync<OrderDetailsDto>(cancellationToken: cancellationToken);
                return new OrderOperationResult<OrderDetailsDto>(true, order, statusCode);
            }

            var (errorMsg, errorCode) = await ParseProblemDetailsAsync(response);
            return new OrderOperationResult<OrderDetailsDto>(false, null, statusCode, errorMsg, errorCode);
        }
        catch (Exception)
        {
            return new OrderOperationResult<OrderDetailsDto>(false, null, 0, "Ошибка сети. Проверьте соединение и повторите попытку.");
        }
    }

    public async Task<OrderOperationResult<OrderDetailsDto>> CancelAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsync($"/api/orders/{orderId}/cancel", null, cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                var order = await response.Content.ReadFromJsonAsync<OrderDetailsDto>(cancellationToken: cancellationToken);
                return new OrderOperationResult<OrderDetailsDto>(true, order, statusCode);
            }

            var (errorMsg, errorCode) = await ParseProblemDetailsAsync(response);
            return new OrderOperationResult<OrderDetailsDto>(false, null, statusCode, errorMsg, errorCode);
        }
        catch (Exception)
        {
            return new OrderOperationResult<OrderDetailsDto>(false, null, 0, "Ошибка сети. Проверьте соединение и повторите попытку.");
        }
    }

    private static async Task<(string ErrorMessage, string? ErrorCode)> ParseProblemDetailsAsync(HttpResponseMessage response)
    {
        return await ApiErrorReader.ReadAsync(response);
    }
}
