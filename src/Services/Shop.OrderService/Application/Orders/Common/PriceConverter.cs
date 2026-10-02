namespace Shop.OrderService.Application.Orders.Common;

public static class PriceConverter
{
    public static decimal FromMinorUnits(long minorUnits)
    {
        if (minorUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minorUnits), "Minor units cannot be negative.");
        }

        return minorUnits / 100m;
    }

    public static long ToMinorUnits(decimal decimalUnits)
    {
        if (decimalUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(decimalUnits), "Decimal units cannot be negative.");
        }

        return (long)Math.Round(decimalUnits * 100m, MidpointRounding.AwayFromZero);
    }
}
