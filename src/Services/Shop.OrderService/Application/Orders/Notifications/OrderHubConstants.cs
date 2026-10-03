namespace Shop.OrderService.Application.Orders.Notifications;

public static class OrderHubGroups
{
    public const string Admins = "admins";

    public static string User(Guid userId) => $"user:{userId}";
}

public static class OrderHubEvents
{
    public const string OrderCreated = "OrderCreated";
    public const string OrderStatusChanged = "OrderStatusChanged";
    public const string PaymentStatusChanged = "PaymentStatusChanged";
}
