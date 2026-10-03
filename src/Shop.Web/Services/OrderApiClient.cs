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
            var response = await _httpClient.PostAsJsonAsync("/api/orders", new CheckoutRequest
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
        catch (Exception ex)
        {
            return new OrderOperationResult<OrderDetailsDto>(false, null, 0, $"Ошибка сети: {ex.Message}");
        }
    }

    public async Task<PagedResult<OrderListItemDto>?> GetOrdersAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/orders?page={page}&pageSize={pageSize}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PagedResult<OrderListItemDto>>(cancellationToken: cancellationToken);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OrderApiClient] GetOrdersAsync error: {ex.Message}");
            return null;
        }
    }

    public async Task<OrderDetailsDto?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/orders/{orderId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<OrderDetailsDto>(cancellationToken: cancellationToken);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OrderApiClient] GetOrderAsync error: {ex.Message}");
            return null;
        }
    }

    public async Task<OrderOperationResult<OrderDetailsDto>> PayAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/orders/{orderId}/pay", null, cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                var order = await response.Content.ReadFromJsonAsync<OrderDetailsDto>(cancellationToken: cancellationToken);
                return new OrderOperationResult<OrderDetailsDto>(true, order, statusCode);
            }

            var (errorMsg, errorCode) = await ParseProblemDetailsAsync(response);
            return new OrderOperationResult<OrderDetailsDto>(false, null, statusCode, errorMsg, errorCode);
        }
        catch (Exception ex)
        {
            return new OrderOperationResult<OrderDetailsDto>(false, null, 0, $"Ошибка сети: {ex.Message}");
        }
    }

    public async Task<OrderOperationResult<OrderDetailsDto>> CancelAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/orders/{orderId}/cancel", null, cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                var order = await response.Content.ReadFromJsonAsync<OrderDetailsDto>(cancellationToken: cancellationToken);
                return new OrderOperationResult<OrderDetailsDto>(true, order, statusCode);
            }

            var (errorMsg, errorCode) = await ParseProblemDetailsAsync(response);
            return new OrderOperationResult<OrderDetailsDto>(false, null, statusCode, errorMsg, errorCode);
        }
        catch (Exception ex)
        {
            return new OrderOperationResult<OrderDetailsDto>(false, null, 0, $"Ошибка сети: {ex.Message}");
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
