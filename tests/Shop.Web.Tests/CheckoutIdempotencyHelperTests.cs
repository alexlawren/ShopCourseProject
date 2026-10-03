using Shop.Web.Services;

namespace Shop.Web.Tests;

public class CheckoutIdempotencyHelperTests
{
    [Theory]
    [InlineData(400, "EMPTY_CART")]
    [InlineData(409, "OUT_OF_STOCK")]
    [InlineData(409, "PRODUCT_NOT_FOUND")]
    [InlineData(409, "PRODUCT_INACTIVE")]
    [InlineData(409, "CATEGORY_INACTIVE")]
    [InlineData(409, "CART_CHANGED")]
    [InlineData(400, "INVALID_REQUEST_ID")]
    [InlineData(400, "CART_TOO_LARGE")]
    public void ShouldClearRequestIdOnFailure_DeterministicBusinessFailures_ReturnsTrue(int statusCode, string errorCode)
    {
        var shouldClear = CheckoutIdempotencyHelper.ShouldClearRequestIdOnFailure(statusCode, errorCode);
        Assert.True(shouldClear);
    }

    [Theory]
    [InlineData(503, "CATALOG_UNAVAILABLE")]
    [InlineData(503, null)]
    [InlineData(502, "DOWNSTREAM_ERROR")]
    [InlineData(502, null)]
    [InlineData(0, null)] // Network timeout / interruption
    public void ShouldClearRequestIdOnFailure_UncertainFailures_ReturnsFalse(int statusCode, string? errorCode)
    {
        var shouldClear = CheckoutIdempotencyHelper.ShouldClearRequestIdOnFailure(statusCode, errorCode);
        Assert.False(shouldClear);
    }
}
