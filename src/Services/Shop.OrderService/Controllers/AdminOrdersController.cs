using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.OrderService.Application.Orders.Dtos;
using Shop.OrderService.Application.Orders.Services;
using Shop.OrderService.Domain.Enums;

namespace Shop.OrderService.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "Admin")]
public sealed class AdminOrdersController : ControllerBase
{
    private readonly IOrderManagementService _orderManagementService;

    public AdminOrdersController(IOrderManagementService orderManagementService)
    {
        _orderManagementService = orderManagementService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminOrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? paymentStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Page must be greater than or equal to 1.",
                extensions: new Dictionary<string, object?> { ["code"] = "INVALID_PAGE" });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "PageSize must be between 1 and 100.",
                extensions: new Dictionary<string, object?> { ["code"] = "INVALID_PAGE_SIZE" });
        }

        OrderStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<OrderStatus>(status, true, out var st))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request",
                    detail: $"Invalid order status: '{status}'.",
                    extensions: new Dictionary<string, object?> { ["code"] = "INVALID_STATUS" });
            }

            parsedStatus = st;
        }

        PaymentStatus? parsedPaymentStatus = null;
        if (!string.IsNullOrWhiteSpace(paymentStatus))
        {
            if (!Enum.TryParse<PaymentStatus>(paymentStatus, true, out var ps))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request",
                    detail: $"Invalid payment status: '{paymentStatus}'.",
                    extensions: new Dictionary<string, object?> { ["code"] = "INVALID_PAYMENT_STATUS" });
            }

            parsedPaymentStatus = ps;
        }

        var result = await _orderManagementService.AdminGetOrdersAsync(
            page, pageSize, parsedStatus, parsedPaymentStatus, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminOrderDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var order = await _orderManagementService.AdminGetOrderByIdAsync(id, cancellationToken);
        if (order == null)
        {
            return NotFound();
        }

        return Ok(order);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(AdminOrderDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateOrderStatus(
        [FromRoute] Guid id,
        [FromBody] AdminUpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Status))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Status is required.",
                extensions: new Dictionary<string, object?> { ["code"] = "INVALID_STATUS" });
        }

        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var nextStatus))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: $"Invalid order status: '{request.Status}'.",
                extensions: new Dictionary<string, object?> { ["code"] = "INVALID_STATUS" });
        }

        if (nextStatus == OrderStatus.Cancelled)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: "Status 'Cancelled' cannot be set via generic status PATCH. Use the cancel endpoint instead.",
                extensions: new Dictionary<string, object?> { ["code"] = "INVALID_ORDER_TRANSITION" });
        }

        var result = await _orderManagementService.AdminUpdateStatusAsync(id, nextStatus, cancellationToken);

        return result.Status switch
        {
            OrderOperationStatus.Success => Ok(result.AdminOrder),

            OrderOperationStatus.NotFound => NotFound(),

            OrderOperationStatus.CancellationInProgress => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: result.ErrorMessage,
                extensions: new Dictionary<string, object?> { ["code"] = "ORDER_CANCELLATION_IN_PROGRESS" }),

            OrderOperationStatus.InvalidTransition => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: result.ErrorMessage,
                extensions: new Dictionary<string, object?> { ["code"] = "INVALID_ORDER_TRANSITION" }),

            _ => Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: result.ErrorMessage ?? "An unexpected error occurred.")
        };
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(AdminOrderDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CancelOrder(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _orderManagementService.CancelOrderAsync(null, id, cancellationToken);

        return result.Status switch
        {
            OrderOperationStatus.Success => Ok(result.AdminOrder ?? (object)result.Order!),

            OrderOperationStatus.NotFound => NotFound(),

            OrderOperationStatus.CannotCancel or OrderOperationStatus.InvalidTransition => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: result.ErrorMessage,
                extensions: new Dictionary<string, object?> { ["code"] = "ORDER_CANNOT_BE_CANCELLED" }),

            OrderOperationStatus.CatalogUnavailable => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service Unavailable",
                detail: result.ErrorMessage,
                extensions: new Dictionary<string, object?> { ["code"] = "CATALOG_UNAVAILABLE" }),

            OrderOperationStatus.DownstreamError => Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Bad Gateway",
                detail: result.ErrorMessage,
                extensions: new Dictionary<string, object?> { ["code"] = "DOWNSTREAM_ERROR" }),

            _ => Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: result.ErrorMessage ?? "An unexpected error occurred during cancellation.")
        };
    }
}
