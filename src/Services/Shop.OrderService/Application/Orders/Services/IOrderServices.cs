using Shop.OrderService.Application.Orders.Dtos;

namespace Shop.OrderService.Application.Orders.Services;

public enum CheckoutStatus
{
    Created,
    Existing,
    BadRequest,
    CartChanged,
    Conflict,
    CatalogUnavailable,
    DownstreamError,
    LocalError
}

public sealed class CheckoutResult
{
    public CheckoutStatus Status { get; init; }
    public OrderDetailsDto? Order { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static CheckoutResult Created(OrderDetailsDto order) =>
        new() { Status = CheckoutStatus.Created, Order = order };

    public static CheckoutResult Existing(OrderDetailsDto order) =>
        new() { Status = CheckoutStatus.Existing, Order = order };

    public static CheckoutResult BadRequest(string code, string message) =>
        new() { Status = CheckoutStatus.BadRequest, ErrorCode = code, ErrorMessage = message };

    public static CheckoutResult CartChanged(string message) =>
        new() { Status = CheckoutStatus.CartChanged, ErrorCode = "CART_CHANGED", ErrorMessage = message };

    public static CheckoutResult Conflict(string code, string message) =>
        new() { Status = CheckoutStatus.Conflict, ErrorCode = code, ErrorMessage = message };

    public static CheckoutResult CatalogUnavailable(string message) =>
        new() { Status = CheckoutStatus.CatalogUnavailable, ErrorCode = "CATALOG_UNAVAILABLE", ErrorMessage = message };

    public static CheckoutResult DownstreamError(string message) =>
        new() { Status = CheckoutStatus.DownstreamError, ErrorCode = "DOWNSTREAM_ERROR", ErrorMessage = message };

    public static CheckoutResult LocalError(string message) =>
        new() { Status = CheckoutStatus.LocalError, ErrorCode = "LOCAL_ERROR", ErrorMessage = message };
}

public interface ICheckoutService
{
    Task<CheckoutResult> CheckoutAsync(Guid userId, CheckoutRequest request, CancellationToken cancellationToken = default);
}

public interface IOrderQueryService
{
    Task<PagedResult<OrderListItemDto>> GetOrdersAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<OrderDetailsDto?> GetOrderByIdAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default);
}
