using Microsoft.EntityFrameworkCore;
using Shop.OrderService.Application.Cart.Dtos;
using Shop.OrderService.Domain.Entities;
using Shop.OrderService.Infrastructure.Persistence;
using CartEntity = Shop.OrderService.Domain.Entities.Cart;

namespace Shop.OrderService.Application.Cart.Services;

public sealed class CartService : ICartService
{
    private readonly OrderDbContext _dbContext;

    public CartService(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CartDto> GetCartAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await _dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart == null)
        {
            var now = DateTime.UtcNow;
            cart = new CartEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Items = new List<CartItem>()
            };

            _dbContext.Carts.Add(cart);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _dbContext.Entry(cart).State = EntityState.Detached;
                cart = await _dbContext.Carts
                    .Include(c => c.Items)
                    .FirstAsync(c => c.UserId == userId, cancellationToken);
            }
        }

        return MapToDto(cart);
    }

    public async Task<CartResult<CartDto>> AddItemAsync(Guid userId, AddCartItemRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var cart = await _dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart == null)
        {
            cart = new CartEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Items = new List<CartItem>()
            };
            _dbContext.Carts.Add(cart);
        }

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
        if (existingItem != null)
        {
            int newQuantity = existingItem.Quantity + request.Quantity;
            if (newQuantity > 1000)
            {
                return CartResult<CartDto>.BadRequest("Total quantity for an item in cart cannot exceed 1000.");
            }

            existingItem.Quantity = newQuantity;
            existingItem.UpdatedAtUtc = now;
        }
        else
        {
            if (request.Quantity > 1000)
            {
                return CartResult<CartDto>.BadRequest("Total quantity for an item in cart cannot exceed 1000.");
            }

            var newItem = new CartItem
            {
                Id = Guid.NewGuid(),
                CartId = cart.Id,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            cart.Items.Add(newItem);
            _dbContext.CartItems.Add(newItem);
        }

        cart.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CartResult<CartDto>.Success(MapToDto(cart));
    }

    public async Task<CartResult<CartDto>> UpdateItemAsync(Guid userId, Guid productId, UpdateCartItemRequest request, CancellationToken cancellationToken = default)
    {
        var cart = await _dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart == null)
        {
            return CartResult<CartDto>.NotFound("Item not found in cart.");
        }

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (existingItem == null)
        {
            return CartResult<CartDto>.NotFound("Item not found in cart.");
        }

        if (request.Quantity <= 0 || request.Quantity > 1000)
        {
            return CartResult<CartDto>.BadRequest("Quantity must be between 1 and 1000.");
        }

        var now = DateTime.UtcNow;
        existingItem.Quantity = request.Quantity;
        existingItem.UpdatedAtUtc = now;
        cart.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return CartResult<CartDto>.Success(MapToDto(cart));
    }

    public async Task<CartResult> RemoveItemAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default)
    {
        var cart = await _dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart == null)
        {
            return CartResult.Success();
        }

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (existingItem != null)
        {
            _dbContext.CartItems.Remove(existingItem);
            cart.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return CartResult.Success();
    }

    public async Task<CartResult> ClearAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await _dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart == null)
        {
            return CartResult.Success();
        }

        if (cart.Items.Count > 0)
        {
            _dbContext.CartItems.RemoveRange(cart.Items);
            cart.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return CartResult.Success();
    }

    private static CartDto MapToDto(CartEntity cart)
    {
        return new CartDto
        {
            Id = cart.Id,
            CreatedAtUtc = cart.CreatedAtUtc,
            UpdatedAtUtc = cart.UpdatedAtUtc,
            Items = cart.Items
                .OrderBy(i => i.CreatedAtUtc)
                .Select(i => new CartItemDto(i.ProductId, i.Quantity))
                .ToList()
        };
    }
}
