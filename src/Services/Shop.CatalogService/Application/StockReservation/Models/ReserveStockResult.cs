namespace Shop.CatalogService.Application.StockReservation.Models;

public sealed record ReservedItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);

public sealed class ReserveStockResult
{
    public bool IsSuccess { get; init; }
    public Guid? ReservationId { get; init; }
    public IReadOnlyList<ReservedItemDto> Items { get; init; } = Array.Empty<ReservedItemDto>();
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static ReserveStockResult Success(Guid reservationId, IReadOnlyList<ReservedItemDto> items)
        => new() { IsSuccess = true, ReservationId = reservationId, Items = items };

    public static ReserveStockResult Failure(string errorCode, string errorMessage)
        => new() { IsSuccess = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
