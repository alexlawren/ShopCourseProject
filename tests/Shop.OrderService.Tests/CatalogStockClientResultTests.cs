using Shop.OrderService.Application.Orders.Services;

namespace Shop.OrderService.Tests;

public class CatalogStockClientResultTests
{
    [Fact]
    public void CatalogReserveResult_Success_StateMatches()
    {
        var resId = Guid.NewGuid();
        var items = new List<CatalogReservedItem>
        {
            new(Guid.NewGuid(), "Item 1", 10.00m, 2, 20.00m)
        };

        var result = CatalogReserveResult.Success(resId, items);

        Assert.True(result.IsSuccess);
        Assert.Equal(CatalogReserveStatus.Success, result.Status);
        Assert.Equal(resId, result.ReservationId);
        Assert.Single(result.Items);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void CatalogReserveResult_BusinessError_StateMatches()
    {
        var result = CatalogReserveResult.BusinessError("OUT_OF_STOCK", "Product is out of stock.");

        Assert.False(result.IsSuccess);
        Assert.Equal(CatalogReserveStatus.BusinessError, result.Status);
        Assert.Equal("OUT_OF_STOCK", result.ErrorCode);
        Assert.Equal("Product is out of stock.", result.ErrorMessage);
    }

    [Fact]
    public void CatalogReserveResult_Unavailable_StateMatches()
    {
        var result = CatalogReserveResult.Unavailable("Connection timeout.");

        Assert.False(result.IsSuccess);
        Assert.Equal(CatalogReserveStatus.Unavailable, result.Status);
        Assert.Equal("CATALOG_UNAVAILABLE", result.ErrorCode);
        Assert.Equal("Connection timeout.", result.ErrorMessage);
    }

    [Fact]
    public void CatalogReserveResult_DownstreamError_StateMatches()
    {
        var result = CatalogReserveResult.DownstreamError("Protocol parsing error.");

        Assert.False(result.IsSuccess);
        Assert.Equal(CatalogReserveStatus.DownstreamError, result.Status);
        Assert.Equal("DOWNSTREAM_ERROR", result.ErrorCode);
        Assert.Equal("Protocol parsing error.", result.ErrorMessage);
    }

    [Fact]
    public void CatalogReleaseResult_States()
    {
        var success = CatalogReleaseResult.Success("Released.");
        Assert.True(success.IsSuccess);
        Assert.Equal(CatalogReleaseStatus.Success, success.Status);

        var notFound = CatalogReleaseResult.NotFound("Not found.");
        Assert.False(notFound.IsSuccess);
        Assert.Equal(CatalogReleaseStatus.NotFound, notFound.Status);

        var alreadyCommitted = CatalogReleaseResult.AlreadyCommitted("Already committed.");
        Assert.False(alreadyCommitted.IsSuccess);
        Assert.Equal(CatalogReleaseStatus.AlreadyCommitted, alreadyCommitted.Status);

        var unavailable = CatalogReleaseResult.Unavailable("Unavailable.");
        Assert.False(unavailable.IsSuccess);
        Assert.Equal(CatalogReleaseStatus.Unavailable, unavailable.Status);
    }

    [Fact]
    public void CatalogCommitResult_States()
    {
        var success = CatalogCommitResult.Success("Committed.");
        Assert.True(success.IsSuccess);
        Assert.Equal(CatalogCommitStatus.Success, success.Status);

        var notFound = CatalogCommitResult.NotFound("Not found.");
        Assert.False(notFound.IsSuccess);
        Assert.Equal(CatalogCommitStatus.NotFound, notFound.Status);

        var alreadyReleased = CatalogCommitResult.AlreadyReleased("Already released.");
        Assert.False(alreadyReleased.IsSuccess);
        Assert.Equal(CatalogCommitStatus.AlreadyReleased, alreadyReleased.Status);

        var unavailable = CatalogCommitResult.Unavailable("Unavailable.");
        Assert.False(unavailable.IsSuccess);
        Assert.Equal(CatalogCommitStatus.Unavailable, unavailable.Status);
    }
}
