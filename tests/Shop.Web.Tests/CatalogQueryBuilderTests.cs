using Shop.Web.Models.Catalog;
using Shop.Web.Services;

namespace Shop.Web.Tests;

public class CatalogQueryBuilderTests
{
    [Fact]
    public void BuildQueryString_DefaultQuery_ReturnsExpectedPaging()
    {
        var query = new CatalogQueryModel();
        var url = CatalogQueryBuilder.BuildQueryString(query);

        Assert.Equal("/api/catalog/products?sort=newest&page=1&pageSize=12", url);
    }

    [Fact]
    public void BuildQueryString_SearchQuery_UrlEncodesSearchTerm()
    {
        var query = new CatalogQueryModel
        {
            Search = "Laptop & Phone + Case"
        };
        var url = CatalogQueryBuilder.BuildQueryString(query);

        Assert.Contains("search=Laptop%20%26%20Phone%20%2B%20Case", url);
    }

    [Fact]
    public void BuildQueryString_CategoryFilter_IncludesCategoryId()
    {
        var categoryId = Guid.NewGuid();
        var query = new CatalogQueryModel
        {
            CategoryId = categoryId
        };
        var url = CatalogQueryBuilder.BuildQueryString(query);

        Assert.Contains($"categoryId={categoryId}", url);
    }

    [Fact]
    public void BuildQueryString_PriceFilters_FormatsWithF2Invariant()
    {
        var query = new CatalogQueryModel
        {
            MinPrice = 12.50m,
            MaxPrice = 99.99m
        };
        var url = CatalogQueryBuilder.BuildQueryString(query);

        Assert.Contains("minPrice=12.50", url);
        Assert.Contains("maxPrice=99.99", url);
    }

    [Fact]
    public void BuildQueryString_InStockFilter_IncludesBoolean()
    {
        var queryTrue = new CatalogQueryModel { InStock = true };
        var urlTrue = CatalogQueryBuilder.BuildQueryString(queryTrue);
        Assert.Contains("inStock=true", urlTrue);

        var queryFalse = new CatalogQueryModel { InStock = false };
        var urlFalse = CatalogQueryBuilder.BuildQueryString(queryFalse);
        Assert.Contains("inStock=false", urlFalse);
    }

    [Fact]
    public void BuildQueryString_SortParam_UrlEncodesSort()
    {
        var query = new CatalogQueryModel { Sort = "price-desc" };
        var url = CatalogQueryBuilder.BuildQueryString(query);
        Assert.Contains("sort=price-desc", url);
    }

    [Fact]
    public void BuildQueryString_AllParametersCombined_BuildsCorrectQuery()
    {
        var categoryId = Guid.NewGuid();
        var query = new CatalogQueryModel
        {
            Search = "Pro",
            CategoryId = categoryId,
            MinPrice = 100m,
            MaxPrice = 500m,
            InStock = true,
            Sort = "name-asc",
            Page = 3,
            PageSize = 24
        };

        var url = CatalogQueryBuilder.BuildQueryString(query);

        Assert.StartsWith("/api/catalog/products?", url);
        Assert.Contains("search=Pro", url);
        Assert.Contains($"categoryId={categoryId}", url);
        Assert.Contains("minPrice=100.00", url);
        Assert.Contains("maxPrice=500.00", url);
        Assert.Contains("inStock=true", url);
        Assert.Contains("sort=name-asc", url);
        Assert.Contains("page=3", url);
        Assert.Contains("pageSize=24", url);
    }
}
