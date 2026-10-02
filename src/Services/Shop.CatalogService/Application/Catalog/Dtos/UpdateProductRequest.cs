using System.ComponentModel.DataAnnotations;

namespace Shop.CatalogService.Application.Catalog.Dtos;

public sealed class UpdateProductRequest
{
    [Required(ErrorMessage = "CategoryId is required.")]
    public Guid CategoryId { get; init; }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; init; } = string.Empty;

    [StringLength(4000, ErrorMessage = "Description cannot exceed 4000 characters.")]
    public string? Description { get; init; }

    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Price must be greater than or equal to 0.")]
    public decimal Price { get; init; }

    public bool IsActive { get; init; } = true;
}
