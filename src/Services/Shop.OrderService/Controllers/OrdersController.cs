using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.OrderService.Application.Orders.Dtos;
using Shop.OrderService.Application.Orders.Services;
using Shop.OrderService.Controllers.Common;

namespace Shop.OrderService.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly ICheckoutService _checkoutService;
    private readonly IOrderQueryService _orderQueryService;
    private readonly IOrderManagementService _orderManagementService;

    public OrdersController(
        ICheckoutService checkoutService,
        IOrderQueryService orderQueryService,
        IOrderManagementService orderManagementService)
    {
        _checkoutService = checkoutService;
        _orderQueryService = orderQueryService;
        _orderManagementService = orderManagementService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderDetailsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(OrderDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (request == null || request.RequestId == Guid.Empty)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "RequestId is required and cannot be empty.",
                extensions: new Dictionary<string, object?> { ["code"] = "INVALID_REQUEST_ID" });
        }

        var result = await _checkoutService.CheckoutAsync(userId, request, cancellationToken);

        return result.Status switch
        {
            CheckoutStatus.Created => CreatedAtAction(
                nameof(GetOrderById),
                new { id = result.Order!.Id },
                result.Order),

            CheckoutStatus.Existing => Ok(result.Order),

            CheckoutStatus.BadRequest => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: result.ErrorMessage,
                extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode ?? "BAD_REQUEST" }),

            CheckoutStatus.CartChanged => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: result.ErrorMessage,
                extensions: new Dictionary<string, object?> { ["code"] = "CART_CHANGED" }),

            CheckoutStatus.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: result.ErrorMessage,
                extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode ?? "CONFLICT" }),

            CheckoutStatus.CatalogUnavailable => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service Unavailable",
                detail: result.ErrorMessage,
                extensions: new Dictionary<string, object?> { ["code"] = "CATALOG_UNAVAILABLE" }),

            CheckoutStatus.DownstreamError => Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Bad Gateway",
                detail: result.ErrorMessage,
                extensions: new Dictionary<string, object?> { ["code"] = "DOWNSTREAM_ERROR" }),

            _ => Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: result.ErrorMessage ?? "An unexpected error occurred.")
        };
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

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

        var orders = await _orderQueryService.GetOrdersAsync(userId, page, pageSize, cancellationToken);
        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var order = await _orderQueryService.GetOrderByIdAsync(userId, id, cancellationToken);
        if (order == null)
        {
            return NotFound();
        }

        return Ok(order);
    }

    [HttpPost("{id:guid}/pay")]
    [ProducesResponseType(typeof(OrderDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PayOrder(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _orderManagementService.PayOrderAsync(userId, id, cancellationToken);

        return result.Status switch
        {
            OrderOperationStatus.Success => Ok(result.Order),

            OrderOperationStatus.NotFound => NotFound(),

            OrderOperationStatus.PaymentCancelled => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: result.ErrorMessage,
                extensions: new Dictionary<string, object?> { ["code"] = "PAYMENT_CANCELLED" }),

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
                detail: result.ErrorMessage ?? "An unexpected error occurred during payment.")
        };
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(OrderDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CancelOrder(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _orderManagementService.CancelOrderAsync(userId, id, cancellationToken);

        return result.Status switch
        {
            OrderOperationStatus.Success => Ok(result.Order),

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
