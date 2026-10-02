using System.ComponentModel.DataAnnotations;

namespace Shop.CatalogService.Application.Catalog.Dtos;

public sealed class UpdateStockRequest
{
    [Range(0, int.MaxValue, ErrorMessage = "Quantity must be greater than or equal to 0.")]
    public int Quantity { get; init; }
}
