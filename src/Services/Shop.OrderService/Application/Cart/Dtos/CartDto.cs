namespace Shop.OrderService.Application.Cart.Dtos;

public sealed class CartDto
{
    public Guid Id { get; init; }
    public IReadOnlyList<CartItemDto> Items { get; init; } = Array.Empty<CartItemDto>();
    public int TotalQuantity => Items.Sum(i => i.Quantity);
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
