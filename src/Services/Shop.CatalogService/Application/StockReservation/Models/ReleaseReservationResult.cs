namespace Shop.CatalogService.Application.StockReservation.Models;

public enum ReleaseResultStatus
{
    Success,
    NotFound,
    AlreadyCommitted
}

public sealed record ReleaseReservationResult(ReleaseResultStatus Status, string? Message = null);
