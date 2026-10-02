using System.ComponentModel.DataAnnotations;

namespace Shop.OrderService.Application.Cart.Dtos;

public sealed class UpdateCartItemRequest
{
    [Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000.")]
    public int Quantity { get; set; }
}
