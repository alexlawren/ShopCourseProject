using Shop.OrderService.Application.Orders.Dtos;
using Shop.OrderService.Domain.Enums;

namespace Shop.OrderService.Application.Orders.Services;

public enum OrderOperationStatus
{
    Success,
    NotFound,
    InvalidTransition,
    PaymentCancelled,
    CancellationInProgress,
    CannotCancel,
    CatalogUnavailable,
    DownstreamError,
    LocalError
}

public sealed class OrderOperationResult
{
    public OrderOperationStatus Status { get; init; }
    public bool IsSuccess => Status == OrderOperationStatus.Success;
    public OrderDetailsDto? Order { get; init; }
    public AdminOrderDetailsDto? AdminOrder { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static OrderOperationResult CancellationInProgress(string message = "Order cancellation is currently in progress.") =>
        new() { Status = OrderOperationStatus.CancellationInProgress, ErrorCode = "ORDER_CANCELLATION_IN_PROGRESS", ErrorMessage = message };

    public static OrderOperationResult Success(OrderDetailsDto order, AdminOrderDetailsDto? adminOrder = null) =>
        new() { Status = OrderOperationStatus.Success, Order = order, AdminOrder = adminOrder };

    public static OrderOperationResult AdminSuccess(AdminOrderDetailsDto adminOrder) =>
        new() { Status = OrderOperationStatus.Success, AdminOrder = adminOrder };

    public static OrderOperationResult NotFound(string message = "Order not found.") =>
        new() { Status = OrderOperationStatus.NotFound, ErrorCode = "ORDER_NOT_FOUND", ErrorMessage = message };

    public static OrderOperationResult InvalidTransition(string message) =>
        new() { Status = OrderOperationStatus.InvalidTransition, ErrorCode = "INVALID_ORDER_TRANSITION", ErrorMessage = message };

    public static OrderOperationResult PaymentCancelled(string message = "Cannot pay for a cancelled order.") =>
        new() { Status = OrderOperationStatus.PaymentCancelled, ErrorCode = "PAYMENT_CANCELLED", ErrorMessage = message };

    public static OrderOperationResult CannotCancel(string message) =>
        new() { Status = OrderOperationStatus.CannotCancel, ErrorCode = "ORDER_CANNOT_BE_CANCELLED", ErrorMessage = message };

    public static OrderOperationResult CatalogUnavailable(string message) =>
        new() { Status = OrderOperationStatus.CatalogUnavailable, ErrorCode = "CATALOG_UNAVAILABLE", ErrorMessage = message };

    public static OrderOperationResult DownstreamError(string message) =>
        new() { Status = OrderOperationStatus.DownstreamError, ErrorCode = "DOWNSTREAM_ERROR", ErrorMessage = message };

    public static OrderOperationResult LocalError(string message) =>
        new() { Status = OrderOperationStatus.LocalError, ErrorCode = "LOCAL_ERROR", ErrorMessage = message };
}

public interface IOrderManagementService
{
    Task<OrderOperationResult> PayOrderAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<OrderOperationResult> CancelOrderAsync(
        Guid? userId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<OrderOperationResult> AdminUpdateStatusAsync(
        Guid orderId,
        OrderStatus nextStatus,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AdminOrderListItemDto>> AdminGetOrdersAsync(
        int page,
        int pageSize,
        OrderStatus? status,
        PaymentStatus? paymentStatus,
        CancellationToken cancellationToken = default);

    Task<AdminOrderDetailsDto?> AdminGetOrderByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}
