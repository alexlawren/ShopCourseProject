namespace Shop.CatalogService.Application.StockReservation.Common;

public static class PriceConverter
{
    public static long ToMinorUnits(decimal price)
    {
        return (long)Math.Round(price * 100m, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal FromMinorUnits(long minorUnits)
    {
        return minorUnits / 100m;
    }
}
