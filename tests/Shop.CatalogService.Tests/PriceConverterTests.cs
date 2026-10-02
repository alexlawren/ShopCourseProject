using Shop.CatalogService.Application.StockReservation.Common;

namespace Shop.CatalogService.Tests;

public class PriceConverterTests
{
    [Theory]
    [InlineData(1500.25, 150025L)]
    [InlineData(0.00, 0L)]
    [InlineData(99.99, 9999L)]
    [InlineData(10.50, 1050L)]
    [InlineData(0.01, 1L)]
    [InlineData(123456.78, 12345678L)]
    public void ToMinorUnits_ConvertsDecimalToCents(decimal price, long expectedMinor)
    {
        var actual = PriceConverter.ToMinorUnits(price);
        Assert.Equal(expectedMinor, actual);
    }

    [Theory]
    [InlineData(150025L, 1500.25)]
    [InlineData(0L, 0.00)]
    [InlineData(9999L, 99.99)]
    [InlineData(1050L, 10.50)]
    [InlineData(1L, 0.01)]
    [InlineData(12345678L, 123456.78)]
    public void FromMinorUnits_ConvertsCentsToDecimal(long minor, decimal expectedPrice)
    {
        var actual = PriceConverter.FromMinorUnits(minor);
        Assert.Equal(expectedPrice, actual);
    }
}
