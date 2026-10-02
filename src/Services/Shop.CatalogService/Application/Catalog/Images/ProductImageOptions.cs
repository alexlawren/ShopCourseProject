namespace Shop.CatalogService.Application.Catalog.Images;

public sealed class ProductImageOptions
{
    public const string SectionName = "ProductImages";

    public string StoragePath { get; set; } = "data/product-images";
    public string RequestPath { get; set; } = "/product-images";
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024; // 5 MiB
}
