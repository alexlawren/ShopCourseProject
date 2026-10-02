using System.ComponentModel.DataAnnotations;

namespace Shop.CatalogService.Application.Catalog.Dtos;

public sealed class CreateCategoryRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(120, ErrorMessage = "Name cannot exceed 120 characters.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "Slug is required.")]
    [StringLength(140, ErrorMessage = "Slug cannot exceed 140 characters.")]
    [RegularExpression(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Slug must contain only lowercase letters, digits, and hyphens (e.g. 'laptops', 'gaming-laptops').")]
    public string Slug { get; init; } = string.Empty;
}
