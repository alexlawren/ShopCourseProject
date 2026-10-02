namespace Shop.CatalogService.Application.StockReservation.Models;

public enum CommitResultStatus
{
    Success,
    NotFound,
    AlreadyReleased
}

public sealed record CommitReservationResult(CommitResultStatus Status, string? Message = null);
