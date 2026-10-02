using Shop.CatalogService.Grpc;
using Shop.OrderService.Application.Orders.Services;

namespace Shop.OrderService.Application.Orders.Common;

public static class ReserveResponseValidator
{
    public static (bool IsValid, string? ErrorMessage, Guid ReservationId, List<CatalogReservedItem> Items) Validate(
        ReserveStockResponse response,
        IReadOnlyList<CatalogReserveItem> requestedItems)
    {
        if (response == null)
        {
            return (false, "Response cannot be null.", Guid.Empty, new());
        }

        if (!Guid.TryParse(response.ReservationId, out var reservationId) || reservationId == Guid.Empty)
        {
            return (false, "Invalid or empty ReservationId in catalog response.", Guid.Empty, new());
        }

        if (response.Items.Count != requestedItems.Count)
        {
            return (false, $"Item count mismatch: requested {requestedItems.Count}, got {response.Items.Count}.", reservationId, new());
        }

        var requestedMap = requestedItems.ToDictionary(i => i.ProductId, i => i.Quantity);
        var seenProductIds = new HashSet<Guid>();
        var validatedItems = new List<CatalogReservedItem>();

        foreach (var item in response.Items)
        {
            if (!Guid.TryParse(item.ProductId, out var productId) || productId == Guid.Empty)
            {
                return (false, "Invalid ProductId in catalog response items.", reservationId, new());
            }

            if (!seenProductIds.Add(productId))
            {
                return (false, $"Duplicate ProductId '{productId}' returned in catalog response.", reservationId, new());
            }

            if (!requestedMap.TryGetValue(productId, out var expectedQty))
            {
                return (false, $"Unexpected ProductId '{productId}' returned in catalog response.", reservationId, new());
            }

            if (item.Quantity != expectedQty)
            {
                return (false, $"Quantity mismatch for product '{productId}': requested {expectedQty}, got {item.Quantity}.", reservationId, new());
            }

            if (item.UnitPriceMinor < 0)
            {
                return (false, $"Negative UnitPriceMinor '{item.UnitPriceMinor}' for product '{productId}'.", reservationId, new());
            }

            decimal unitPrice;
            try
            {
                unitPrice = PriceConverter.FromMinorUnits(item.UnitPriceMinor);
            }
            catch (Exception ex)
            {
                return (false, $"Invalid UnitPriceMinor '{item.UnitPriceMinor}': {ex.Message}", reservationId, new());
            }

            var lineTotal = unitPrice * item.Quantity;
            validatedItems.Add(new CatalogReservedItem(
                productId,
                item.ProductName ?? string.Empty,
                unitPrice,
                item.Quantity,
                lineTotal));
        }

        return (true, null, reservationId, validatedItems);
    }
}
