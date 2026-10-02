namespace Shop.CatalogService.Application.Catalog.Queries;

public sealed class ProductQueryParameters
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const string DefaultSort = "newest";

    public static readonly string[] SupportedSorts =
    [
        "priceAsc",
        "priceDesc",
        "nameAsc",
        "nameDesc",
        "newest"
    ];

    public string? Search { get; init; }
    public Guid? CategoryId { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool? InStock { get; init; }
    public string? Sort { get; init; } = DefaultSort;
    public int Page { get; init; } = DefaultPage;
    public int PageSize { get; init; } = DefaultPageSize;
}
