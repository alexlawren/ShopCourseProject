using Shop.Web.Models.Cart;

namespace Shop.Web.Services;

public static class CartTotalHelper
{
    public static decimal CalculatePreviewTotal(IEnumerable<CartItemViewModel>? items)
    {
        if (items == null)
            return 0m;

        return items
            .Where(i => i.IsAvailable && i.UnitPrice.HasValue && i.Quantity > 0)
            .Sum(i => i.UnitPrice!.Value * i.Quantity);
    }
}
