namespace Shop.Web.Services;

public static class AdminOrderUiHelper
{
    public static string? GetNextAllowedStatus(string? currentStatus) => currentStatus?.ToLowerInvariant() switch
    {
        "created" => "Confirmed",
        "confirmed" => "Processing",
        "processing" => "Shipped",
        "shipped" => "Completed",
        _ => null // Completed and Cancelled are terminal; others have no generic forward action
    };

    public static string GetNextStatusActionName(string? currentStatus) => currentStatus?.ToLowerInvariant() switch
    {
        "created" => "Подтвердить заказ (Confirmed)",
        "confirmed" => "Перевести в обработку (Processing)",
        "processing" => "Отправить заказ (Shipped)",
        "shipped" => "Завершить заказ (Completed)",
        _ => string.Empty
    };

    public static bool CanAdminCancel(string? currentStatus) =>
        OrderUiHelper.CanCancel(currentStatus);

    public static string GetStatusBadgeClass(string? status) =>
        OrderUiHelper.GetStatusBadgeClass(status);

    public static string GetStatusDisplayName(string? status) =>
        OrderUiHelper.GetStatusDisplayName(status);

    public static string GetPaymentStatusBadgeClass(string? paymentStatus) =>
        OrderUiHelper.GetPaymentStatusBadgeClass(paymentStatus);

    public static string GetPaymentStatusDisplayName(string? paymentStatus) =>
        OrderUiHelper.GetPaymentStatusDisplayName(paymentStatus);
}
