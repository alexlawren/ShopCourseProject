using Shop.Web.Models.Cart;
using Shop.Web.Services;

namespace Shop.Web.Tests;

public class CartTotalHelperTests
{
    [Fact]
    public void CalculatePreviewTotal_NullOrEmptyItems_ReturnsZero()
    {
        Assert.Equal(0m, CartTotalHelper.CalculatePreviewTotal(null));
        Assert.Equal(0m, CartTotalHelper.CalculatePreviewTotal([]));
    }

    [Fact]
    public void CalculatePreviewTotal_ValidItems_CalculatesAccurateDecimalSum()
    {
        var items = new List<CartItemViewModel>
        {
            new()
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Product A",
                UnitPrice = 199.99m,
                Quantity = 2,
                IsAvailable = true
            },
            new()
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Product B",
                UnitPrice = 50.50m,
                Quantity = 3,
                IsAvailable = true
            }
        };

        // (199.99 * 2) + (50.50 * 3) = 399.98 + 151.50 = 551.48m
        var total = CartTotalHelper.CalculatePreviewTotal(items);
        Assert.Equal(551.48m, total);
    }

    [Fact]
    public void CalculatePreviewTotal_UnavailableOrNullPriceItems_ExcludedFromTotal()
    {
        var items = new List<CartItemViewModel>
        {
            new()
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Available Product",
                UnitPrice = 100m,
                Quantity = 2,
                IsAvailable = true
            },
            new()
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Unavailable Product",
                UnitPrice = 500m,
                Quantity = 1,
                IsAvailable = false
            },
            new()
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Missing Price Product",
                UnitPrice = null,
                Quantity = 1,
                IsAvailable = true
            }
        };

        var total = CartTotalHelper.CalculatePreviewTotal(items);
        Assert.Equal(200m, total);
    }
}
