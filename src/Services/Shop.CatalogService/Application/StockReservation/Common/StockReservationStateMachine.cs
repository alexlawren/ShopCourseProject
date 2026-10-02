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
            _ => false
        };

    public static bool CanRelease(StockReservationStatus currentStatus) =>
        currentStatus switch
        {
            StockReservationStatus.Reserved => true,
            StockReservationStatus.Released => true, // idempotent
            StockReservationStatus.Committed => false,
            _ => false
        };
}
