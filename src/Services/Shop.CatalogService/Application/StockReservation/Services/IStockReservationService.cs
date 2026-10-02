using Shop.CatalogService.Application.StockReservation.Models;

namespace Shop.CatalogService.Application.StockReservation.Services;

public interface IStockReservationService
{
    Task<ReserveStockResult> ReserveStockAsync(
        Guid requestId,
        IReadOnlyList<(Guid ProductId, int Quantity)> items,
        CancellationToken cancellationToken = default);

    Task<ReleaseReservationResult> ReleaseReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<CommitReservationResult> CommitReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);
}
