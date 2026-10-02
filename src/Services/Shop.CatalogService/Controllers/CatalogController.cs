using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shop.CatalogService.Application.Catalog.Dtos;
using Shop.CatalogService.Application.Catalog.Queries;
using Shop.CatalogService.Application.Catalog.Services;

namespace Shop.CatalogService.Controllers;

[ApiController]
[Route("api/catalog")]
public sealed class CatalogController : ControllerBase
{
    private readonly ICatalogQueryService _queryService;

    public CatalogController(ICatalogQueryService queryService)
    {
        _queryService = queryService;
    }

    /// <summary>
    /// Returns all active categories sorted alphabetically by name.
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await _queryService.GetCategoriesAsync(cancellationToken);
        return Ok(categories);
    }

    /// <summary>
    /// Returns paginated list of active products with search, filtering, and sorting support.
    /// </summary>
    [HttpGet("products")]
    [ProducesResponseType(typeof(PagedResult<ProductListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> GetProducts(
        [FromQuery] ProductQueryParameters query,
        CancellationToken cancellationToken)
    {
        var errors = ProductQueryValidator.Validate(query);
        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Detail = "The product query parameters are invalid."
            });
        }

        var result = await _queryService.GetProductsAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns product details by ID if both product and its category are active.
    /// </summary>
    [HttpGet("products/{id:guid}")]
    [ProducesResponseType(typeof(ProductDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailsDto>> GetProductById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _queryService.GetProductByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product Not Found",
                detail: $"Product with ID '{id}' was not found or is inactive.");
        }

        return Ok(product);
    }
}
