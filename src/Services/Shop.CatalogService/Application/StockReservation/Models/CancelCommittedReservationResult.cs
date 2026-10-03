namespace Shop.CatalogService.Application.StockReservation.Models;

public enum CancelCommittedResultStatus
{
    Success,
    NotFound,
    InvalidState
}

public sealed record CancelCommittedReservationResult(CancelCommittedResultStatus Status, string? Message = null);
