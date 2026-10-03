using Shop.CatalogService.Application.Catalog.Queries;

namespace Shop.CatalogService.Tests;

public class AdminProductQueryValidatorTests
{
    [Fact]
    public void Validate_WithDefaultParameters_ReturnsNoErrors()
    {
        var parameters = new AdminProductQueryParameters();

        var errors = AdminProductQueryValidator.Validate(parameters);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public void Validate_WhenPageLessThanOne_ReturnsPageError(int page)
    {
        var parameters = new AdminProductQueryParameters { Page = page };

        var errors = AdminProductQueryValidator.Validate(parameters);

        Assert.True(errors.ContainsKey(nameof(AdminProductQueryParameters.Page)));
        Assert.Contains(errors[nameof(AdminProductQueryParameters.Page)], msg => msg.Contains("Page must be greater than or equal to 1"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenPageSizeLessThanOne_ReturnsPageSizeError(int pageSize)
    {
        var parameters = new AdminProductQueryParameters { PageSize = pageSize };

        var errors = AdminProductQueryValidator.Validate(parameters);

        Assert.True(errors.ContainsKey(nameof(AdminProductQueryParameters.PageSize)));
        Assert.Contains(errors[nameof(AdminProductQueryParameters.PageSize)], msg => msg.Contains("PageSize must be greater than or equal to 1"));
    }

    [Theory]
    [InlineData(101)]
    [InlineData(200)]
    public void Validate_WhenPageSizeExceedsMaximum_ReturnsPageSizeError(int pageSize)
    {
        var parameters = new AdminProductQueryParameters { PageSize = pageSize };

        var errors = AdminProductQueryValidator.Validate(parameters);

        Assert.True(errors.ContainsKey(nameof(AdminProductQueryParameters.PageSize)));
        Assert.Contains(errors[nameof(AdminProductQueryParameters.PageSize)], msg => msg.Contains("must not exceed"));
    }

    [Theory]
    [InlineData("newest")]
    [InlineData("priceAsc")]
    [InlineData("priceDesc")]
    [InlineData("nameAsc")]
    [InlineData("nameDesc")]
    [InlineData("NEWEST")]
    public void Validate_WithSupportedSorts_ReturnsNoErrors(string sort)
    {
        var parameters = new AdminProductQueryParameters { Sort = sort };

        var errors = AdminProductQueryValidator.Validate(parameters);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("date")]
    [InlineData("popular")]
    public void Validate_WithUnsupportedSort_ReturnsSortError(string sort)
    {
        var parameters = new AdminProductQueryParameters { Sort = sort };

        var errors = AdminProductQueryValidator.Validate(parameters);

        Assert.True(errors.ContainsKey(nameof(AdminProductQueryParameters.Sort)));
        Assert.Contains(errors[nameof(AdminProductQueryParameters.Sort)], msg => msg.Contains("is not supported"));
    }
}
