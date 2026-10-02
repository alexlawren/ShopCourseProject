using Shop.CatalogService.Grpc;
using Shop.OrderService.Application.Orders.Common;
using Shop.OrderService.Application.Orders.Services;

namespace Shop.OrderService.Tests;

public class ReserveResponseValidatorTests
{
    private readonly Guid _prod1 = Guid.NewGuid();
    private readonly Guid _prod2 = Guid.NewGuid();
    private readonly Guid _reservationId = Guid.NewGuid();

    [Fact]
    public void Validate_MatchingProductAndQuantity_Accepted()
    {
        var requested = new List<CatalogReserveItem>
        {
            new(_prod1, 2),
            new(_prod2, 1)
        };

        var response = new ReserveStockResponse
        {
            Success = true,
            ReservationId = _reservationId.ToString(),
            Items =
            {
                new ReservedStockItem
                {
                    ProductId = _prod1.ToString(),
                    ProductName = "Product 1",
                    UnitPriceMinor = 10050,
                    Quantity = 2
                },
                new ReservedStockItem
                {
                    ProductId = _prod2.ToString(),
                    ProductName = "Product 2",
                    UnitPriceMinor = 2500,
                    Quantity = 1
                }
            }
        };

        var (isValid, errorMessage, reservationId, items) = ReserveResponseValidator.Validate(response, requested);

        Assert.True(isValid);
        Assert.Null(errorMessage);
        Assert.Equal(_reservationId, reservationId);
        Assert.Equal(2, items.Count);

        var item1 = items.First(i => i.ProductId == _prod1);
        Assert.Equal("Product 1", item1.ProductName);
        Assert.Equal(100.50m, item1.UnitPrice);
        Assert.Equal(2, item1.Quantity);
        Assert.Equal(201.00m, item1.LineTotal);

        var item2 = items.First(i => i.ProductId == _prod2);
        Assert.Equal("Product 2", item2.ProductName);
        Assert.Equal(25.00m, item2.UnitPrice);
        Assert.Equal(1, item2.Quantity);
        Assert.Equal(25.00m, item2.LineTotal);
    }

    [Fact]
    public void Validate_MissingProduct_Rejected()
    {
        var requested = new List<CatalogReserveItem>
        {
            new(_prod1, 2),
            new(_prod2, 1)
        };

        var response = new ReserveStockResponse
        {
            Success = true,
            ReservationId = _reservationId.ToString(),
            Items =
            {
                new ReservedStockItem
                {
                    ProductId = _prod1.ToString(),
                    ProductName = "Product 1",
                    UnitPriceMinor = 10050,
                    Quantity = 2
                }
            }
        };

        var (isValid, errorMessage, _, _) = ReserveResponseValidator.Validate(response, requested);

        Assert.False(isValid);
        Assert.Contains("Item count mismatch", errorMessage);
    }

    [Fact]
    public void Validate_DuplicateReturnedProduct_Rejected()
    {
        var requested = new List<CatalogReserveItem>
        {
            new(_prod1, 2),
            new(_prod2, 1)
        };

        var response = new ReserveStockResponse
        {
            Success = true,
            ReservationId = _reservationId.ToString(),
            Items =
            {
                new ReservedStockItem
                {
                    ProductId = _prod1.ToString(),
                    ProductName = "Product 1",
                    UnitPriceMinor = 10050,
                    Quantity = 2
                },
                new ReservedStockItem
                {
                    ProductId = _prod1.ToString(),
                    ProductName = "Product 1 Duplicate",
                    UnitPriceMinor = 10050,
                    Quantity = 1
                }
            }
        };

        var (isValid, errorMessage, _, _) = ReserveResponseValidator.Validate(response, requested);

        Assert.False(isValid);
        Assert.Contains("Duplicate ProductId", errorMessage);
    }

    [Fact]
    public void Validate_QuantityMismatch_Rejected()
    {
        var requested = new List<CatalogReserveItem>
        {
            new(_prod1, 2)
        };

        var response = new ReserveStockResponse
        {
            Success = true,
            ReservationId = _reservationId.ToString(),
            Items =
            {
                new ReservedStockItem
                {
                    ProductId = _prod1.ToString(),
                    ProductName = "Product 1",
                    UnitPriceMinor = 10050,
                    Quantity = 1 // Expected 2
                }
            }
        };

        var (isValid, errorMessage, _, _) = ReserveResponseValidator.Validate(response, requested);

        Assert.False(isValid);
        Assert.Contains("Quantity mismatch", errorMessage);
    }

    [Fact]
    public void Validate_NegativeUnitPriceMinor_Rejected()
    {
        var requested = new List<CatalogReserveItem>
        {
            new(_prod1, 1)
        };

        var response = new ReserveStockResponse
        {
            Success = true,
            ReservationId = _reservationId.ToString(),
            Items =
            {
                new ReservedStockItem
                {
                    ProductId = _prod1.ToString(),
                    ProductName = "Product 1",
                    UnitPriceMinor = -50,
                    Quantity = 1
                }
            }
        };

        var (isValid, errorMessage, _, _) = ReserveResponseValidator.Validate(response, requested);

        Assert.False(isValid);
        Assert.Contains("Negative UnitPriceMinor", errorMessage);
    }

    [Fact]
    public void Validate_InvalidReservationId_Rejected()
    {
        var requested = new List<CatalogReserveItem>
        {
            new(_prod1, 1)
        };

        var response = new ReserveStockResponse
        {
            Success = true,
            ReservationId = "not-a-guid",
            Items =
            {
                new ReservedStockItem
                {
                    ProductId = _prod1.ToString(),
                    ProductName = "Product 1",
                    UnitPriceMinor = 100,
                    Quantity = 1
                }
            }
        };

        var (isValid, errorMessage, _, _) = ReserveResponseValidator.Validate(response, requested);

        Assert.False(isValid);
        Assert.Contains("Invalid or empty ReservationId", errorMessage);
    }
}
