namespace Shop.Web.Services;

public static class OrderUiHelper
{
    public static bool CanCancel(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        return string.Equals(status, "Created", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Processing", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetStatusBadgeClass(string? status) => status?.ToLowerInvariant() switch
    {
        "created" => "badge bg-secondary",
        "confirmed" => "badge bg-primary",
        "processing" => "badge bg-info text-dark",
        "shipped" => "badge bg-warning text-dark",
        "completed" => "badge bg-success",
        "cancelled" => "badge bg-danger",
        _ => "badge bg-light text-dark"
    };

    public static string GetStatusDisplayName(string? status) => status?.ToLowerInvariant() switch
    {
        "created" => "Создан",
        "confirmed" => "Подтверждён",
        "processing" => "В обработке",
        "shipped" => "Отправлен",
        "completed" => "Выполнен",
        "cancelled" => "Отменён",
        _ => status ?? string.Empty
    };

    public static string GetPaymentStatusBadgeClass(string? paymentStatus) => paymentStatus?.ToLowerInvariant() switch
    {
        "pending" => "badge bg-warning text-dark",
        "paid" => "badge bg-success",
        "cancelled" => "badge bg-danger",
        _ => "badge bg-light text-dark"
    };

    public static string GetPaymentStatusDisplayName(string? paymentStatus) => paymentStatus?.ToLowerInvariant() switch
    {
        "pending" => "Ожидает оплаты",
        "paid" => "Оплачен",
        "cancelled" => "Отменён",
        _ => paymentStatus ?? string.Empty
    };
}
