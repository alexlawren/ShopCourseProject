using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shop.CatalogService.Application.Catalog.Commands;
using Shop.CatalogService.Application.Catalog.Dtos;
using Shop.CatalogService.Application.Catalog.Queries;
using Shop.CatalogService.Application.Catalog.Services;
using Shop.CatalogService.Domain.Constants;

namespace Shop.CatalogService.Controllers;

[ApiController]
[Route("api/catalog")]
public sealed class CatalogController : ControllerBase
{
    private readonly ICatalogQueryService _queryService;
    private readonly ICatalogCommandService _commandService;

    public CatalogController(
        ICatalogQueryService queryService,
        ICatalogCommandService commandService)
    {
        _queryService = queryService;
        _commandService = commandService;
    }

    // ==========================================
    // Public Read Endpoints
    // ==========================================

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

    // ==========================================
    // Admin Category Write Endpoints
    // ==========================================

    /// <summary>
    /// Creates a new category. Requires Admin role.
    /// </summary>
    [HttpPost("categories")]
    [Authorize(Roles = CatalogRoles.Admin)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> CreateCategory(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _commandService.CreateCategoryAsync(request, cancellationToken);

        if (result.Status == CommandStatus.Conflict)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: result.ErrorMessage);
        }

        return CreatedAtAction(
            nameof(GetCategories),
            new { id = result.Value!.Id },
            result.Value);
    }

    /// <summary>
    /// Updates an existing category. Requires Admin role.
    /// </summary>
    [HttpPut("categories/{id:guid}")]
    [Authorize(Roles = CatalogRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCategory(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _commandService.UpdateCategoryAsync(id, request, cancellationToken);

        return result.Status switch
        {
            CommandStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Category Not Found",
                detail: result.ErrorMessage),

            CommandStatus.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: result.ErrorMessage),

            _ => NoContent()
        };
    }

    /// <summary>
    /// Soft-deletes a category by setting IsActive = false. Requires Admin role.
    /// </summary>
    [HttpDelete("categories/{id:guid}")]
    [Authorize(Roles = CatalogRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _commandService.DeleteCategoryAsync(id, cancellationToken);

        if (result.Status == CommandStatus.NotFound)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Category Not Found",
                detail: result.ErrorMessage);
        }

        return NoContent();
    }

    // ==========================================
    // Admin Product Write Endpoints
    // ==========================================

    /// <summary>
    /// Creates a new product. Requires Admin role.
    /// </summary>
    [HttpPost("products")]
    [Authorize(Roles = CatalogRoles.Admin)]
    [ProducesResponseType(typeof(ProductDetailsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProductDetailsDto>> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _commandService.CreateProductAsync(request, cancellationToken);

        if (result.Status == CommandStatus.BadRequest)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: result.ErrorMessage);
        }

        return CreatedAtAction(
            nameof(GetProductById),
            new { id = result.Value!.Id },
            result.Value);
    }

    /// <summary>
    /// Updates product details (name, description, price, category, is-active). Does not update stock quantity or image path. Requires Admin role.
    /// </summary>
    [HttpPut("products/{id:guid}")]
    [Authorize(Roles = CatalogRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _commandService.UpdateProductAsync(id, request, cancellationToken);

        return result.Status switch
        {
            CommandStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product Not Found",
                detail: result.ErrorMessage),

            CommandStatus.BadRequest => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: result.ErrorMessage),

            _ => NoContent()
        };
    }

    /// <summary>
    /// Soft-deletes a product by setting IsActive = false. Requires Admin role.
    /// </summary>
    [HttpDelete("products/{id:guid}")]
    [Authorize(Roles = CatalogRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _commandService.DeleteProductAsync(id, cancellationToken);

        if (result.Status == CommandStatus.NotFound)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product Not Found",
                detail: result.ErrorMessage);
        }

        return NoContent();
    }

    /// <summary>
    /// Sets absolute current stock quantity for a product. Requires Admin role.
    /// </summary>
    [HttpPatch("products/{id:guid}/stock")]
    [Authorize(Roles = CatalogRoles.Admin)]
    [ProducesResponseType(typeof(StockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockDto>> UpdateProductStock(
        Guid id,
        [FromBody] UpdateStockRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _commandService.UpdateProductStockAsync(id, request.Quantity, cancellationToken);

        return result.Status switch
        {
            CommandStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product Not Found",
                detail: result.ErrorMessage),

            CommandStatus.BadRequest => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: result.ErrorMessage),

            _ => Ok(result.Value)
        };
    }
}
