namespace Shop.Web.Models.Cart;

public class CartItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

public class CartDto
{
    public Guid Id { get; set; }
    public IReadOnlyList<CartItemDto> Items { get; set; } = [];
    public int TotalQuantity => Items.Sum(i => i.Quantity);
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class AddCartItemRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class UpdateCartItemRequest
{
    public int Quantity { get; set; }
}

public class CartItemViewModel
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal? UnitPrice { get; set; }
    public string? ImagePath { get; set; }
    public bool IsAvailable { get; set; } = true;
    public decimal? LineTotal => UnitPrice.HasValue ? UnitPrice.Value * Quantity : null;
}
