using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shop.OrderService.Application.Orders.Dtos;
using Shop.OrderService.Domain.Entities;
using Shop.OrderService.Domain.Enums;
using Shop.OrderService.Infrastructure.Persistence;

namespace Shop.OrderService.Application.Orders.Services;

public sealed class CheckoutService : ICheckoutService
{
    private readonly OrderDbContext _dbContext;
    private readonly ICatalogStockClient _catalogStockClient;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        OrderDbContext dbContext,
        ICatalogStockClient catalogStockClient,
        ILogger<CheckoutService> logger)
    {
        _dbContext = dbContext;
        _catalogStockClient = catalogStockClient;
        _logger = logger;
    }

    public async Task<CheckoutResult> CheckoutAsync(
        Guid userId,
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || request.RequestId == Guid.Empty)
        {
            return CheckoutResult.BadRequest("INVALID_REQUEST_ID", "RequestId is required and cannot be empty.");
        }

        // 1. Idempotent fast-path: return existing Order if already processed for this RequestId
        var existingOrder = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.CheckoutRequestId == request.RequestId, cancellationToken);

        if (existingOrder != null)
        {
            if (existingOrder.UserId != userId)
            {
                _logger.LogWarning("Checkout RequestId {RequestId} belongs to user {ExistingUserId}, not {CurrentUserId}.",
                    request.RequestId, existingOrder.UserId, userId);
                return CheckoutResult.Conflict("REQUEST_ID_CONFLICT", "Checkout request ID belongs to another user.");
            }

            // Retry commit in case network response was lost previously
            var commitRetry = await _catalogStockClient.CommitReservationAsync(existingOrder.ReservationId, cancellationToken);
            if (commitRetry.Status == CatalogCommitStatus.Unavailable)
            {
                return CheckoutResult.CatalogUnavailable("Catalog service is unavailable. Please retry.");
            }

            return CheckoutResult.Existing(MapToDetailsDto(existingOrder));
        }

        // 2. Load and validate current user cart
        var cart = await _dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart == null || cart.Items.Count == 0)
        {
            return CheckoutResult.BadRequest("EMPTY_CART", "Cannot checkout with an empty cart.");
        }

        if (cart.Items.Count > 100)
        {
            return CheckoutResult.BadRequest("CART_TOO_LARGE", "Cart cannot contain more than 100 items.");
        }

        // 3. Create immutable cart snapshot
        var snapshot = cart.Items
            .Select(i => new CatalogReserveItem(i.ProductId, i.Quantity))
            .ToList();

        // 4. Reserve stock via gRPC
        var reserveResult = await _catalogStockClient.ReserveStockAsync(request.RequestId, snapshot, cancellationToken);
        if (!reserveResult.IsSuccess)
        {
            if (reserveResult.Status == CatalogReserveStatus.Unavailable)
            {
                return CheckoutResult.CatalogUnavailable(reserveResult.ErrorMessage ?? "Catalog service is currently unavailable.");
            }

            if (reserveResult.Status == CatalogReserveStatus.DownstreamError)
            {
                return CheckoutResult.DownstreamError(reserveResult.ErrorMessage ?? "Catalog downstream protocol error.");
            }

            return CheckoutResult.Conflict(
                reserveResult.ErrorCode ?? "RESERVATION_FAILED",
                reserveResult.ErrorMessage ?? "Stock reservation failed.");
        }

        // 5. Verify cart did not change concurrently while calling CatalogService
        var currentItems = await _dbContext.CartItems
            .AsNoTracking()
            .Where(ci => ci.CartId == cart.Id)
            .ToListAsync(cancellationToken);

        bool cartChanged = currentItems.Count != snapshot.Count ||
            snapshot.Any(s =>
            {
                var found = currentItems.FirstOrDefault(ci => ci.ProductId == s.ProductId);
                return found == null || found.Quantity != s.Quantity;
            });

        if (cartChanged)
        {
            // Check if another concurrent request with the same RequestId already won and created the order (and cleared the cart)
            var winnerOrder = await _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .Include(o => o.StatusHistory)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.CheckoutRequestId == request.RequestId, cancellationToken);

            if (winnerOrder != null && winnerOrder.UserId == userId)
            {
                // The concurrent winner already created the order and cleared the cart!
                // DO NOT release reservation: the winner owns it!
                await _catalogStockClient.CommitReservationAsync(winnerOrder.ReservationId, cancellationToken);
                return CheckoutResult.Existing(MapToDetailsDto(winnerOrder));
            }

            if (winnerOrder != null && winnerOrder.UserId != userId)
            {
                return CheckoutResult.Conflict("REQUEST_ID_CONFLICT", "Checkout request ID belongs to another user.");
            }

            _logger.LogWarning("Cart for user {UserId} changed concurrently during checkout {RequestId}. Compensating reservation {ReservationId}.",
                userId, request.RequestId, reserveResult.ReservationId);

            await _catalogStockClient.ReleaseReservationAsync(reserveResult.ReservationId, cancellationToken);
            return CheckoutResult.CartChanged("Cart was modified concurrently. Please review your cart and retry checkout.");
        }

        // 6. Build Order and snapshots
        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CheckoutRequestId = request.RequestId,
            ReservationId = reserveResult.ReservationId,
            Status = OrderStatus.Created,
            PaymentStatus = PaymentStatus.Pending,
            TotalAmount = reserveResult.Items.Sum(i => i.LineTotal),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        foreach (var item in reserveResult.Items)
        {
            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                LineTotal = item.LineTotal
            });
        }

        order.StatusHistory.Add(new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Status = OrderStatus.Created,
            ChangedAtUtc = now
        });

        // 7. Atomic local transaction: save Order and clear CartItems
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.Orders.Add(order);
            _dbContext.CartItems.RemoveRange(cart.Items);
            cart.UpdatedAtUtc = now;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            // Check if concurrent duplicate checkout won the local insert
            var winnerOrder = await _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .Include(o => o.StatusHistory)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.CheckoutRequestId == request.RequestId, cancellationToken);

            if (winnerOrder != null && winnerOrder.UserId == userId)
            {
                _logger.LogInformation("Concurrent checkout duplicate won by another request for RequestId {RequestId}.", request.RequestId);
                // DO NOT release reservation: the winner owns it!
                var commitRetry = await _catalogStockClient.CommitReservationAsync(winnerOrder.ReservationId, cancellationToken);
                if (commitRetry.Status == CatalogCommitStatus.Unavailable)
                {
                    return CheckoutResult.CatalogUnavailable("Catalog service unavailable during confirmation. Please retry.");
                }

                return CheckoutResult.Existing(MapToDetailsDto(winnerOrder));
            }

            if (winnerOrder != null && winnerOrder.UserId != userId)
            {
                return CheckoutResult.Conflict("REQUEST_ID_CONFLICT", "Checkout request ID belongs to another user.");
            }

            // Other unexpected DB failure: compensate reservation
            _logger.LogError(ex, "Failed to save order locally for RequestId {RequestId}. Compensating reservation {ReservationId}.",
                request.RequestId, reserveResult.ReservationId);

            await _catalogStockClient.ReleaseReservationAsync(reserveResult.ReservationId, cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(ex, "Unexpected error saving order locally for RequestId {RequestId}. Compensating reservation {ReservationId}.",
                request.RequestId, reserveResult.ReservationId);

            await _catalogStockClient.ReleaseReservationAsync(reserveResult.ReservationId, cancellationToken);
            throw;
        }

        // 8. Commit reservation in CatalogService (Order is now durable)
        var commitResult = await _catalogStockClient.CommitReservationAsync(order.ReservationId, cancellationToken);
        if (commitResult.Status == CatalogCommitStatus.Unavailable)
        {
            // CRITICAL: Durable order exists. DO NOT RELEASE. DO NOT DELETE ORDER.
            _logger.LogWarning("Order {OrderId} was saved, but Catalog Commit timed out or was unavailable. Client can retry with RequestId {RequestId}.",
                order.Id, request.RequestId);

            return CheckoutResult.CatalogUnavailable("Order was placed, but catalog confirmation timed out. Please retry with the same RequestId.");
        }

        return CheckoutResult.Created(MapToDetailsDto(order));
    }

    public static OrderDetailsDto MapToDetailsDto(Order order)
    {
        return new OrderDetailsDto
        {
            Id = order.Id,
            Status = order.Status.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            TotalAmount = order.TotalAmount,
            CreatedAtUtc = order.CreatedAtUtc,
            UpdatedAtUtc = order.UpdatedAtUtc,
            Items = order.Items.Select(i => new OrderItemDto(
                i.ProductId,
                i.ProductName,
                i.UnitPrice,
                i.Quantity,
                i.LineTotal)).ToList(),
            StatusHistory = order.StatusHistory
                .OrderBy(sh => sh.ChangedAtUtc)
                .Select(sh => new OrderStatusHistoryDto(sh.Status.ToString(), sh.ChangedAtUtc))
                .ToList()
        };
    }
}
