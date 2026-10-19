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
        return await ApiReadHelper.GetAsync<PagedResult<AdminOrderListItemDto>>(_httpClient, url, cancellationToken);
    }

    public async Task<AdminOrderDetailsDto?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await ApiReadHelper.GetAsync<AdminOrderDetailsDto>(_httpClient, $"/api/admin/orders/{id}", cancellationToken, allowNotFound: true);
    }

    public async Task<AdminOperationResult<AdminOrderDetailsDto>> UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PatchAsJsonAsync($"/api/admin/orders/{id}/status", new AdminUpdateOrderStatusRequest
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
        catch (Exception)
        {
            return AdminOperationResult<AdminOrderDetailsDto>.Failure("Ошибка при изменении статуса заказа. Проверьте соединение и повторите попытку.");
        }
    }

    public async Task<AdminOperationResult<AdminOrderDetailsDto>> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsync($"/api/admin/orders/{id}/cancel", null, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var order = await response.Content.ReadFromJsonAsync<AdminOrderDetailsDto>(cancellationToken: cancellationToken);
                return AdminOperationResult<AdminOrderDetailsDto>.Success(order!);
            }

            var (error, code) = await ParseProblemDetailsAsync(response, cancellationToken);
            return AdminOperationResult<AdminOrderDetailsDto>.Failure(error, code);
        }
        catch (Exception)
        {
            return AdminOperationResult<AdminOrderDetailsDto>.Failure("Ошибка при отмене заказа администратором. Проверьте соединение и повторите попытку.");
        }
    }

    private static async Task<(string ErrorMessage, string? ErrorCode)> ParseProblemDetailsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await ApiErrorReader.ReadAsync(response, cancellationToken);
    }
}
