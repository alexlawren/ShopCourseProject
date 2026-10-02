namespace Shop.OrderService.Domain.Enums;

public enum OrderStatus
{
    Created,
    Confirmed,
    Processing,
    Shipped,
    Completed,
    Cancelled
}
