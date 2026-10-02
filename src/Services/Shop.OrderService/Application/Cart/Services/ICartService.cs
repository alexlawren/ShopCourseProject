using Shop.OrderService.Application.Cart.Dtos;

namespace Shop.OrderService.Application.Cart.Services;

public interface ICartService
{
    Task<CartDto> GetCartAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CartResult<CartDto>> AddItemAsync(Guid userId, AddCartItemRequest request, CancellationToken cancellationToken = default);
    Task<CartResult<CartDto>> UpdateItemAsync(Guid userId, Guid productId, UpdateCartItemRequest request, CancellationToken cancellationToken = default);
    Task<CartResult> RemoveItemAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default);
    Task<CartResult> ClearAsync(Guid userId, CancellationToken cancellationToken = default);
}
