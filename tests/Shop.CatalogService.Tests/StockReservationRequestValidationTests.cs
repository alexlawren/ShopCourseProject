using Shop.CatalogService.Application.StockReservation.Common;
using Shop.CatalogService.Grpc;

namespace Shop.CatalogService.Tests;

public class StockReservationRequestValidationTests
{
    [Fact]
    public void Validate_ValidRequest_ReturnsValid()
    {
        var request = new ReserveStockRequest
        {
            RequestId = Guid.NewGuid().ToString()
        };
        request.Items.Add(new ReserveStockItem { ProductId = Guid.NewGuid().ToString(), Quantity = 5 });
        request.Items.Add(new ReserveStockItem { ProductId = Guid.NewGuid().ToString(), Quantity = 10 });

        var result = StockReservationRequestValidator.Validate(request);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Validate_InvalidRequestId_ReturnsInvalid(string requestId)
    {
        var request = new ReserveStockRequest
        {
            RequestId = requestId
        };
        request.Items.Add(new ReserveStockItem { ProductId = Guid.NewGuid().ToString(), Quantity = 1 });

        var result = StockReservationRequestValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("request_id", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_EmptyItems_ReturnsInvalid()
    {
        var request = new ReserveStockRequest
        {
            RequestId = Guid.NewGuid().ToString()
        };

        var result = StockReservationRequestValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("Items count", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_ZeroOrNegativeQuantity_ReturnsInvalid(int quantity)
    {
        var request = new ReserveStockRequest
        {
            RequestId = Guid.NewGuid().ToString()
        };
        request.Items.Add(new ReserveStockItem { ProductId = Guid.NewGuid().ToString(), Quantity = quantity });

        var result = StockReservationRequestValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("Quantity", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_QuantityGreaterThan1000_ReturnsInvalid()
    {
        var request = new ReserveStockRequest
        {
            RequestId = Guid.NewGuid().ToString()
        };
        request.Items.Add(new ReserveStockItem { ProductId = Guid.NewGuid().ToString(), Quantity = 1001 });

        var result = StockReservationRequestValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("Quantity", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_DuplicateProductId_ReturnsInvalid()
    {
        var duplicateId = Guid.NewGuid().ToString();
        var request = new ReserveStockRequest
        {
            RequestId = Guid.NewGuid().ToString()
        };
        request.Items.Add(new ReserveStockItem { ProductId = duplicateId, Quantity = 2 });
        request.Items.Add(new ReserveStockItem { ProductId = duplicateId, Quantity = 3 });

        var result = StockReservationRequestValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("Duplicate", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_MoreThan100Items_ReturnsInvalid()
    {
        var request = new ReserveStockRequest
        {
            RequestId = Guid.NewGuid().ToString()
        };
        for (int i = 0; i < 101; i++)
        {
            request.Items.Add(new ReserveStockItem { ProductId = Guid.NewGuid().ToString(), Quantity = 1 });
        }

        var result = StockReservationRequestValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("Items count", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Release_ValidReservationId_ReturnsValid()
    {
        var request = new ReleaseReservationRequest
        {
            ReservationId = Guid.NewGuid().ToString()
        };

        var result = StockReservationRequestValidator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Validate_Release_InvalidReservationId_ReturnsInvalid(string reservationId)
    {
        var request = new ReleaseReservationRequest
        {
            ReservationId = reservationId
        };

        var result = StockReservationRequestValidator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_Commit_ValidReservationId_ReturnsValid()
    {
        var request = new CommitReservationRequest
        {
            ReservationId = Guid.NewGuid().ToString()
        };

        var result = StockReservationRequestValidator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Validate_Commit_InvalidReservationId_ReturnsInvalid(string reservationId)
    {
        var request = new CommitReservationRequest
        {
            ReservationId = reservationId
        };

        var result = StockReservationRequestValidator.Validate(request);

        Assert.False(result.IsValid);
    }
}
