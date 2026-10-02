using Shop.CatalogService.Application.Catalog.Dtos;

namespace Shop.CatalogService.Tests;

public class PagedResultTests
{
    [Fact]
    public void Constructor_CalculatesTotalPagesZero_WhenTotalItemsZero()
    {
        // Arrange & Act
        var result = new PagedResult<string>(Array.Empty<string>(), page: 1, pageSize: 20, totalItems: 0);

        // Assert
        Assert.Equal(0, result.TotalPages);
        Assert.Equal(0, result.TotalItems);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData(20, 20, 1)]
    [InlineData(40, 20, 2)]
    [InlineData(100, 10, 10)]
    public void Constructor_CalculatesTotalPages_WhenExactMultiple(int totalItems, int pageSize, int expectedPages)
    {
        // Arrange
        var items = new List<int>();

        // Act
        var result = new PagedResult<int>(items, page: 1, pageSize: pageSize, totalItems: totalItems);

        // Assert
        Assert.Equal(expectedPages, result.TotalPages);
        Assert.Equal(totalItems, result.TotalItems);
    }

    [Theory]
    [InlineData(21, 20, 2)]
    [InlineData(53, 20, 3)]
    [InlineData(1, 20, 1)]
    [InlineData(101, 10, 11)]
    public void Constructor_CalculatesTotalPages_WithCeilingOnRemainder(int totalItems, int pageSize, int expectedPages)
    {
        // Arrange
        var items = new List<string>();

        // Act
        var result = new PagedResult<string>(items, page: 1, pageSize: pageSize, totalItems: totalItems);

        // Assert
        Assert.Equal(expectedPages, result.TotalPages);
    }
}
