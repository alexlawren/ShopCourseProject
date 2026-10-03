using Shop.CatalogService.Domain.Enums;

namespace Shop.CatalogService.Application.StockReservation.Common;

public static class StockReservationStateMachine
{
    public static bool CanCommit(StockReservationStatus currentStatus) =>
        currentStatus switch
        {
            StockReservationStatus.Reserved => true,
            StockReservationStatus.Committed => true, // idempotent
            StockReservationStatus.Released => false,
            StockReservationStatus.Cancelled => false,
            _ => false
        };

    public static bool CanRelease(StockReservationStatus currentStatus) =>
        currentStatus switch
        {
            StockReservationStatus.Reserved => true,
            StockReservationStatus.Released => true, // idempotent
            StockReservationStatus.Committed => false,
            StockReservationStatus.Cancelled => false,
            _ => false
        };

    public static bool CanCancelCommitted(StockReservationStatus currentStatus) =>
        currentStatus switch
        {
            StockReservationStatus.Committed => true,
            StockReservationStatus.Cancelled => true, // idempotent
            StockReservationStatus.Reserved => false,
            StockReservationStatus.Released => false,
            _ => false
        };
}
