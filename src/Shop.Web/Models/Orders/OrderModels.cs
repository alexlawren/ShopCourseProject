namespace Shop.Web.Models.Orders;

public class CheckoutRequest
{
    public Guid RequestId { get; set; }
}

public class OrderItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class OrderStatusHistoryDto
{
    public string Status { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
}

public class OrderDetailsDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public IReadOnlyList<OrderItemDto> Items { get; set; } = [];
    public IReadOnlyList<OrderStatusHistoryDto> StatusHistory { get; set; } = [];
}

public class OrderListItemDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public class OrderCreatedEvent
{
    public Guid OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class OrderStatusChangedEvent
{
    public Guid OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
}

public class PaymentStatusChangedEvent
{
    public Guid OrderId { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
}
