using System.Net.Http.Json;
using Shop.Web.Models.Admin;
using Shop.Web.Models.Auth;
using Shop.Web.Models.Orders;

namespace Shop.Web.Services;

public interface IAdminOrderApiClient
{
    Task<PagedResult<AdminOrderListItemDto>?> GetOrdersAsync(
        int page = 1,
        int pageSize = 20,
        string? status = null,
        string? paymentStatus = null,
        CancellationToken cancellationToken = default);

    Task<AdminOrderDetailsDto?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdminOperationResult<AdminOrderDetailsDto>> UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
    Task<AdminOperationResult<AdminOrderDetailsDto>> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}

public class AdminOrderApiClient : IAdminOrderApiClient
{
    private readonly HttpClient _httpClient;

    public AdminOrderApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResult<AdminOrderListItemDto>?> GetOrdersAsync(
        int page = 1,
        int pageSize = 20,
        string? status = null,
        string? paymentStatus = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryParams = new List<string>
            {
                $"page={page}",
                $"pageSize={pageSize}"
            };

            if (!string.IsNullOrWhiteSpace(status))
            {
                queryParams.Add($"status={Uri.EscapeDataString(status.Trim())}");
            }

            if (!string.IsNullOrWhiteSpace(paymentStatus))
            {
                queryParams.Add($"paymentStatus={Uri.EscapeDataString(paymentStatus.Trim())}");
            }

            var url = $"/api/admin/orders?{string.Join("&", queryParams)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PagedResult<AdminOrderListItemDto>>(cancellationToken: cancellationToken);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminOrderApiClient] GetOrdersAsync error: {ex.Message}");
            return null;
        }
    }

    public async Task<AdminOrderDetailsDto?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/admin/orders/{id}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AdminOrderDetailsDto>(cancellationToken: cancellationToken);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminOrderApiClient] GetOrderAsync error: {ex.Message}");
            return null;
        }
    }

    public async Task<AdminOperationResult<AdminOrderDetailsDto>> UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PatchAsJsonAsync($"/api/admin/orders/{id}/status", new AdminUpdateOrderStatusRequest
            {
                Status = status
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var order = await response.Content.ReadFromJsonAsync<AdminOrderDetailsDto>(cancellationToken: cancellationToken);
                return AdminOperationResult<AdminOrderDetailsDto>.Success(order!);
            }

            var (error, code) = await ParseProblemDetailsAsync(response, cancellationToken);
            return AdminOperationResult<AdminOrderDetailsDto>.Failure(error, code);
        }
        catch (Exception ex)
        {
            return AdminOperationResult<AdminOrderDetailsDto>.Failure($"Ошибка при изменении статуса заказа: {ex.Message}");
        }
    }

    public async Task<AdminOperationResult<AdminOrderDetailsDto>> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/admin/orders/{id}/cancel", null, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var order = await response.Content.ReadFromJsonAsync<AdminOrderDetailsDto>(cancellationToken: cancellationToken);
                return AdminOperationResult<AdminOrderDetailsDto>.Success(order!);
            }

            var (error, code) = await ParseProblemDetailsAsync(response, cancellationToken);
            return AdminOperationResult<AdminOrderDetailsDto>.Failure(error, code);
        }
        catch (Exception ex)
        {
            return AdminOperationResult<AdminOrderDetailsDto>.Failure($"Ошибка при отмене заказа администратором: {ex.Message}");
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
            System.Net.HttpStatusCode.NotFound => ("Заказ не найден.", "NOT_FOUND"),
            System.Net.HttpStatusCode.Conflict => ("Конфликт при изменении заказа.", "CONFLICT"),
            _ => ($"Ошибка сервера ({(int)response.StatusCode}).", response.StatusCode.ToString())
        };
    }
}
