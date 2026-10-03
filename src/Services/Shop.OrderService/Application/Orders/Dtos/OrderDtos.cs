namespace Shop.OrderService.Application.Orders.Dtos;

public sealed class CheckoutRequest
{
    public Guid RequestId { get; set; }
}

public sealed record OrderItemDto(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record OrderStatusHistoryDto(
    string Status,
    DateTime ChangedAtUtc);

public sealed class OrderDetailsDto
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public IReadOnlyList<OrderItemDto> Items { get; init; } = Array.Empty<OrderItemDto>();
    public IReadOnlyList<OrderStatusHistoryDto> StatusHistory { get; init; } = Array.Empty<OrderStatusHistoryDto>();
}

public sealed class OrderListItemDto
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public sealed class AdminOrderListItemDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class AdminOrderDetailsDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public IReadOnlyList<OrderItemDto> Items { get; init; } = Array.Empty<OrderItemDto>();
    public IReadOnlyList<OrderStatusHistoryDto> StatusHistory { get; init; } = Array.Empty<OrderStatusHistoryDto>();
}

public sealed class AdminUpdateOrderStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

