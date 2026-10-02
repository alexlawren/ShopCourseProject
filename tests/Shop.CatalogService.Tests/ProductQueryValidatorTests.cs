using Shop.CatalogService.Application.Catalog.Queries;

namespace Shop.CatalogService.Tests;

public class ProductQueryValidatorTests
{
    [Fact]
    public void Validate_WithDefaultParameters_ReturnsNoErrors()
    {
        // Arrange
        var parameters = new ProductQueryParameters();

        // Act
        var errors = ProductQueryValidator.Validate(parameters);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Validate_WhenPageLessThanOne_ReturnsPageError(int page)
    {
        // Arrange
        var parameters = new ProductQueryParameters { Page = page };

        // Act
        var errors = ProductQueryValidator.Validate(parameters);

        // Assert
        Assert.True(errors.ContainsKey(nameof(ProductQueryParameters.Page)));
        Assert.Contains(errors[nameof(ProductQueryParameters.Page)], msg => msg.Contains("Page must be greater than or equal to 1"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_WhenPageSizeLessThanOne_ReturnsPageSizeError(int pageSize)
    {
        // Arrange
        var parameters = new ProductQueryParameters { PageSize = pageSize };

        // Act
        var errors = ProductQueryValidator.Validate(parameters);

        // Assert
        Assert.True(errors.ContainsKey(nameof(ProductQueryParameters.PageSize)));
        Assert.Contains(errors[nameof(ProductQueryParameters.PageSize)], msg => msg.Contains("PageSize must be greater than or equal to 1"));
    }

    [Theory]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(1000)]
    public void Validate_WhenPageSizeExceedsMaximum_ReturnsPageSizeError(int pageSize)
    {
        // Arrange
        var parameters = new ProductQueryParameters { PageSize = pageSize };

        // Act
        var errors = ProductQueryValidator.Validate(parameters);

        // Assert
        Assert.True(errors.ContainsKey(nameof(ProductQueryParameters.PageSize)));
        Assert.Contains(errors[nameof(ProductQueryParameters.PageSize)], msg => msg.Contains("must not exceed 100"));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Validate_WhenMinPriceNegative_ReturnsMinPriceError(double minPrice)
    {
        // Arrange
        var parameters = new ProductQueryParameters { MinPrice = (decimal)minPrice };

        // Act
        var errors = ProductQueryValidator.Validate(parameters);

        // Assert
        Assert.True(errors.ContainsKey(nameof(ProductQueryParameters.MinPrice)));
        Assert.Contains(errors[nameof(ProductQueryParameters.MinPrice)], msg => msg.Contains("MinPrice must be non-negative"));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-50)]
    public void Validate_WhenMaxPriceNegative_ReturnsMaxPriceError(double maxPrice)
    {
        // Arrange
        var parameters = new ProductQueryParameters { MaxPrice = (decimal)maxPrice };

        // Act
        var errors = ProductQueryValidator.Validate(parameters);

        // Assert
        Assert.True(errors.ContainsKey(nameof(ProductQueryParameters.MaxPrice)));
        Assert.Contains(errors[nameof(ProductQueryParameters.MaxPrice)], msg => msg.Contains("MaxPrice must be non-negative"));
    }

    [Fact]
    public void Validate_WhenMinPriceGreaterThanMaxPrice_ReturnsMinPriceError()
    {
        // Arrange
        var parameters = new ProductQueryParameters
        {
            MinPrice = 100m,
            MaxPrice = 50m
        };

        // Act
        var errors = ProductQueryValidator.Validate(parameters);

        // Assert
        Assert.True(errors.ContainsKey(nameof(ProductQueryParameters.MinPrice)));
        Assert.Contains(errors[nameof(ProductQueryParameters.MinPrice)], msg => msg.Contains("MinPrice cannot be greater than MaxPrice"));
    }

    [Fact]
    public void Validate_WhenMinPriceEqualsMaxPrice_ReturnsNoErrors()
    {
        // Arrange
        var parameters = new ProductQueryParameters
        {
            MinPrice = 100m,
            MaxPrice = 100m
        };

        // Act
        var errors = ProductQueryValidator.Validate(parameters);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("priceAsc")]
    [InlineData("priceDesc")]
    [InlineData("nameAsc")]
    [InlineData("nameDesc")]
    [InlineData("newest")]
    [InlineData("PRICEASC")]
    [InlineData("Newest")]
    public void Validate_WithSupportedSortOptions_ReturnsNoErrors(string sort)
    {
        // Arrange
        var parameters = new ProductQueryParameters { Sort = sort };

        // Act
        var errors = ProductQueryValidator.Validate(parameters);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("invalidSort")]
    [InlineData("id")]
    [InlineData("date")]
    public void Validate_WithUnsupportedSort_ReturnsSortError(string sort)
    {
        // Arrange
        var parameters = new ProductQueryParameters { Sort = sort };

        // Act
        var errors = ProductQueryValidator.Validate(parameters);

        // Assert
        Assert.True(errors.ContainsKey(nameof(ProductQueryParameters.Sort)));
        Assert.Contains(errors[nameof(ProductQueryParameters.Sort)], msg => msg.Contains("not supported"));
    }

    [Fact]
    public void Validate_WithMultipleInvalidParameters_ReturnsAllErrors()
    {
        // Arrange
        var parameters = new ProductQueryParameters
        {
            Page = -1,
            PageSize = 150,
            MinPrice = -10,
            MaxPrice = -20,
            Sort = "invalid"
        };

        // Act
        var errors = ProductQueryValidator.Validate(parameters);

        // Assert
        Assert.True(errors.ContainsKey(nameof(ProductQueryParameters.Page)));
        Assert.True(errors.ContainsKey(nameof(ProductQueryParameters.PageSize)));
        Assert.True(errors.ContainsKey(nameof(ProductQueryParameters.MinPrice)));
        Assert.True(errors.ContainsKey(nameof(ProductQueryParameters.MaxPrice)));
        Assert.True(errors.ContainsKey(nameof(ProductQueryParameters.Sort)));
    }
}
