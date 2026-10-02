using Shop.OrderService.Application.Orders.Common;

namespace Shop.OrderService.Tests;

public class PriceConverterTests
{
    [Theory]
    [InlineData(10050, 100.50)]
    [InlineData(2500, 25.00)]
    [InlineData(0, 0.00)]
    [InlineData(99, 0.99)]
    [InlineData(150025, 1500.25)]
    public void FromMinorUnits_ValidValue_ConvertsCorrectly(long minorUnits, decimal expectedDecimal)
    {
        var result = PriceConverter.FromMinorUnits(minorUnits);
        Assert.Equal(expectedDecimal, result);
    }

    [Fact]
    public void FromMinorUnits_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PriceConverter.FromMinorUnits(-1));
    }

    [Theory]
    [InlineData(100.50, 10050)]
    [InlineData(25.00, 2500)]
    [InlineData(0.00, 0)]
    [InlineData(0.99, 99)]
    public void ToMinorUnits_ValidValue_ConvertsCorrectly(decimal decimalUnits, long expectedMinor)
    {
        var result = PriceConverter.ToMinorUnits(decimalUnits);
        Assert.Equal(expectedMinor, result);
    }

    [Fact]
    public void TotalCalculation_MatchesExpectedExactAmount()
    {
        // 10050 minor * 2 + 2500 minor * 1 = 201.00 + 25.00 = 226.00
        var item1Price = PriceConverter.FromMinorUnits(10050);
        var item1Qty = 2;
        var item1Total = item1Price * item1Qty;

        var item2Price = PriceConverter.FromMinorUnits(2500);
        var item2Qty = 1;
        var item2Total = item2Price * item2Qty;

        var grandTotal = item1Total + item2Total;

        Assert.Equal(201.00m, item1Total);
        Assert.Equal(25.00m, item2Total);
        Assert.Equal(226.00m, grandTotal);
    }
}
