using Shop.CatalogService.Domain.Enums;

namespace Shop.CatalogService.Domain.Entities;

public sealed class StockReservation
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public StockReservationStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<StockReservationItem> Items { get; set; } = new List<StockReservationItem>();
}
