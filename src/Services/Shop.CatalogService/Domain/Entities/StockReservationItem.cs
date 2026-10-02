namespace Shop.CatalogService.Domain.Entities;

public sealed class StockReservationItem
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    public StockReservation Reservation { get; set; } = null!;
}
