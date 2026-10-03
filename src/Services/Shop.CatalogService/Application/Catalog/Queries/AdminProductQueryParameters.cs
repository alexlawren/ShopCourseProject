namespace Shop.CatalogService.Application.Catalog.Queries;

public sealed class AdminProductQueryParameters
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const string DefaultSort = "newest";

    public static readonly string[] SupportedSorts =
    [
        "newest",
        "priceasc",
        "pricedesc",
        "nameasc",
        "namedesc"
    ];

    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = DefaultPage;
    public int PageSize { get; set; } = DefaultPageSize;
    public string? Sort { get; set; } = DefaultSort;
}
