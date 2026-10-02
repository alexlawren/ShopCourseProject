namespace Shop.OrderService.Application.Cart.Dtos;

public sealed record CartItemDto(Guid ProductId, int Quantity);
