using System.ComponentModel.DataAnnotations;

namespace Shop.OrderService.Application.Cart.Dtos;

public sealed class AddCartItemRequest : IValidatableObject
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000.")]
    public int Quantity { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProductId == Guid.Empty)
        {
            yield return new ValidationResult("ProductId must not be empty.", new[] { nameof(ProductId) });
        }
    }
}
