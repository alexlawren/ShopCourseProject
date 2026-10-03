using Shop.Web.Models.Orders;

namespace Shop.Web.Models.Admin;

public class AdminOrderListItemDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AdminOrderDetailsDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public IReadOnlyList<OrderItemDto> Items { get; set; } = [];
    public IReadOnlyList<OrderStatusHistoryDto> StatusHistory { get; set; } = [];
}

public class AdminUpdateOrderStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
