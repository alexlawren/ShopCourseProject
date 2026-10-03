using Microsoft.EntityFrameworkCore;
using Shop.CatalogService.Application.Catalog.Notifications;
using Shop.CatalogService.Application.StockReservation.Models;
using Shop.CatalogService.Domain.Entities;
using Shop.CatalogService.Domain.Enums;
using Shop.CatalogService.Infrastructure.Persistence;
using StockReservationEntity = Shop.CatalogService.Domain.Entities.StockReservation;

namespace Shop.CatalogService.Application.StockReservation.Services;

public sealed class StockReservationService : IStockReservationService
{
    private readonly CatalogDbContext _dbContext;
    private readonly ICatalogNotificationService _notificationService;

    public StockReservationService(
        CatalogDbContext dbContext,
        ICatalogNotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task<ReserveStockResult> ReserveStockAsync(
        Guid requestId,
        IReadOnlyList<(Guid ProductId, int Quantity)> items,
        CancellationToken cancellationToken = default)
    {
        // 1. Initial Idempotency check: return existing reservation if RequestId was already processed
        var existingReservation = await _dbContext.StockReservations
            .AsNoTracking()
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.RequestId == requestId, cancellationToken);

        if (existingReservation != null)
        {
            return ReserveStockResult.Success(
                existingReservation.Id,
                existingReservation.Items
                    .Select(i => new ReservedItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity))
                    .ToList());
        }

        // 2. Open transaction for atomic verification and stock decrement
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .Include(p => p.Category)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        // 3. Pre-validate all items
        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ReserveStockResult.Failure("PRODUCT_NOT_FOUND", $"Product '{item.ProductId}' was not found.");
            }

            if (!product.IsActive)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ReserveStockResult.Failure("PRODUCT_INACTIVE", $"Product '{product.Name}' is inactive.");
            }

            if (!product.Category.IsActive)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ReserveStockResult.Failure("CATEGORY_INACTIVE", $"Category for product '{product.Name}' is inactive.");
            }

            if (product.StockQuantity < item.Quantity)
            {
                await transaction.RollbackAsync(cancellationToken);

                // Check if a concurrent request with the same RequestId already won and reserved this stock
                var concurrentWinner = await _dbContext.StockReservations
                    .AsNoTracking()
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.RequestId == requestId, cancellationToken);

                if (concurrentWinner != null)
                {
                    return ReserveStockResult.Success(
                        concurrentWinner.Id,
                        concurrentWinner.Items
                            .Select(i => new ReservedItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity))
                            .ToList());
                }

                return ReserveStockResult.Failure(
                    "OUT_OF_STOCK",
                    $"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}, requested: {item.Quantity}.");
            }
        }

        // 4. Concurrent-safe conditional update for each item
        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            int affected = await _dbContext.Products
                .Where(p => p.Id == item.ProductId && p.StockQuantity >= item.Quantity && p.IsActive)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.StockQuantity, p => p.StockQuantity - item.Quantity)
                    .SetProperty(p => p.UpdatedAtUtc, now), cancellationToken);

            if (affected != 1)
            {
                await transaction.RollbackAsync(cancellationToken);

                // If conditional update failed, check if a concurrent request with the same RequestId won
                var concurrentWinner = await _dbContext.StockReservations
                    .AsNoTracking()
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.RequestId == requestId, cancellationToken);

                if (concurrentWinner != null)
                {
                    return ReserveStockResult.Success(
                        concurrentWinner.Id,
                        concurrentWinner.Items
                            .Select(i => new ReservedItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity))
                            .ToList());
                }

                return ReserveStockResult.Failure(
                    "OUT_OF_STOCK",
                    $"Failed to reserve stock for product '{item.ProductId}' due to concurrent modification or insufficient stock.");
            }
        }

        // 5. Create reservation and items snapshot
        var reservation = new StockReservationEntity
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            Status = StockReservationStatus.Reserved,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        foreach (var item in items)
        {
            var product = products[item.ProductId];
            var resItem = new StockReservationItem
            {
                Id = Guid.NewGuid(),
                ReservationId = reservation.Id,
                ProductId = item.ProductId,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = item.Quantity
            };
            reservation.Items.Add(resItem);
            _dbContext.StockReservationItems.Add(resItem);
        }

        _dbContext.StockReservations.Add(reservation);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var updatedProducts = await _dbContext.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.StockQuantity, p.UpdatedAtUtc })
                .ToListAsync(cancellationToken);

            foreach (var p in updatedProducts)
            {
                await _notificationService.NotifyStockChangedAsync(
                    new StockChangedEvent(p.Id, p.StockQuantity, p.UpdatedAtUtc),
                    cancellationToken);
            }
        }
        catch (DbUpdateException)
        {
            // Unique constraint violation on RequestId: concurrent duplicate request committed first!
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            var concurrentWinner = await _dbContext.StockReservations
                .AsNoTracking()
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.RequestId == requestId, cancellationToken);

            if (concurrentWinner != null)
            {
                return ReserveStockResult.Success(
                    concurrentWinner.Id,
                    concurrentWinner.Items
                        .Select(i => new ReservedItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity))
                        .ToList());
            }

            throw;
        }

        return ReserveStockResult.Success(
            reservation.Id,
            reservation.Items
                .Select(i => new ReservedItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity))
                .ToList());
    }

    public async Task<ReleaseReservationResult> ReleaseReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;

        // Atomically transition status from Reserved to Released
        int affected = await _dbContext.StockReservations
            .Where(r => r.Id == reservationId && r.Status == StockReservationStatus.Reserved)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, StockReservationStatus.Released)
                .SetProperty(r => r.UpdatedAtUtc, now), cancellationToken);

        if (affected == 1)
        {
            // Exactly one caller wins the transition. Restore stock.
            var items = await _dbContext.StockReservationItems
                .AsNoTracking()
                .Where(i => i.ReservationId == reservationId)
                .ToListAsync(cancellationToken);

            foreach (var item in items)
            {
                await _dbContext.Products
                    .Where(p => p.Id == item.ProductId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.StockQuantity, p => p.StockQuantity + item.Quantity)
                        .SetProperty(p => p.UpdatedAtUtc, now), cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            var releasedProductIds = items.Select(i => i.ProductId).Distinct().ToList();
            var updatedProducts = await _dbContext.Products
                .AsNoTracking()
                .Where(p => releasedProductIds.Contains(p.Id))
                .Select(p => new { p.Id, p.StockQuantity, p.UpdatedAtUtc })
                .ToListAsync(cancellationToken);

            foreach (var p in updatedProducts)
            {
                await _notificationService.NotifyStockChangedAsync(
                    new StockChangedEvent(p.Id, p.StockQuantity, p.UpdatedAtUtc),
                    cancellationToken);
            }

            return new ReleaseReservationResult(ReleaseResultStatus.Success, "Reservation released successfully.");
        }

        // Did not transition (either not found, or not in Reserved status)
        await transaction.RollbackAsync(cancellationToken);

        var currentReservation = await _dbContext.StockReservations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

        if (currentReservation == null)
        {
            return new ReleaseReservationResult(ReleaseResultStatus.NotFound, "Reservation not found.");
        }

        if (currentReservation.Status == StockReservationStatus.Released)
        {
            return new ReleaseReservationResult(ReleaseResultStatus.Success, "Reservation is already released.");
        }

        if (currentReservation.Status == StockReservationStatus.Committed)
        {
            return new ReleaseReservationResult(ReleaseResultStatus.AlreadyCommitted, "Cannot release a committed reservation.");
        }

        return new ReleaseReservationResult(ReleaseResultStatus.AlreadyCommitted, "Reservation state conflict.");
    }

    public async Task<CommitReservationResult> CommitReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;

        // Atomically transition status from Reserved to Committed
        int affected = await _dbContext.StockReservations
            .Where(r => r.Id == reservationId && r.Status == StockReservationStatus.Reserved)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, StockReservationStatus.Committed)
                .SetProperty(r => r.UpdatedAtUtc, now), cancellationToken);

        if (affected == 1)
        {
            await transaction.CommitAsync(cancellationToken);
            return new CommitReservationResult(CommitResultStatus.Success, "Reservation committed successfully.");
        }

        // Did not transition (either not found, or not in Reserved status)
        await transaction.RollbackAsync(cancellationToken);

        var currentReservation = await _dbContext.StockReservations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

        if (currentReservation == null)
        {
            return new CommitReservationResult(CommitResultStatus.NotFound, "Reservation not found.");
        }

        if (currentReservation.Status == StockReservationStatus.Committed)
        {
            return new CommitReservationResult(CommitResultStatus.Success, "Reservation is already committed.");
        }

        if (currentReservation.Status == StockReservationStatus.Released)
        {
            return new CommitReservationResult(CommitResultStatus.AlreadyReleased, "Cannot commit a released reservation.");
        }

        return new CommitReservationResult(CommitResultStatus.AlreadyReleased, "Reservation state conflict.");
    }

    public async Task<CancelCommittedReservationResult> CancelCommittedReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;

        // Atomically transition status from Committed to Cancelled
        int affected = await _dbContext.StockReservations
            .Where(r => r.Id == reservationId && r.Status == StockReservationStatus.Committed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, StockReservationStatus.Cancelled)
                .SetProperty(r => r.UpdatedAtUtc, now), cancellationToken);

        if (affected == 1)
        {
            // Exactly one caller wins the transition. Restore stock.
            var items = await _dbContext.StockReservationItems
                .AsNoTracking()
                .Where(i => i.ReservationId == reservationId)
                .ToListAsync(cancellationToken);

            foreach (var item in items)
            {
                await _dbContext.Products
                    .Where(p => p.Id == item.ProductId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.StockQuantity, p => p.StockQuantity + item.Quantity)
                        .SetProperty(p => p.UpdatedAtUtc, now), cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            var cancelledProductIds = items.Select(i => i.ProductId).Distinct().ToList();
            var updatedProducts = await _dbContext.Products
                .AsNoTracking()
                .Where(p => cancelledProductIds.Contains(p.Id))
                .Select(p => new { p.Id, p.StockQuantity, p.UpdatedAtUtc })
                .ToListAsync(cancellationToken);

            foreach (var p in updatedProducts)
            {
                await _notificationService.NotifyStockChangedAsync(
                    new StockChangedEvent(p.Id, p.StockQuantity, p.UpdatedAtUtc),
                    cancellationToken);
            }

            return new CancelCommittedReservationResult(CancelCommittedResultStatus.Success, "Committed reservation cancelled and stock restored successfully.");
        }

        // Did not transition (either not found, or not in Committed status)
        await transaction.RollbackAsync(cancellationToken);

        var currentReservation = await _dbContext.StockReservations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

        if (currentReservation == null)
        {
            return new CancelCommittedReservationResult(CancelCommittedResultStatus.NotFound, "Reservation not found.");
        }

        if (currentReservation.Status == StockReservationStatus.Cancelled)
        {
            return new CancelCommittedReservationResult(CancelCommittedResultStatus.Success, "Reservation is already cancelled.");
        }

        if (currentReservation.Status == StockReservationStatus.Reserved)
        {
            return new CancelCommittedReservationResult(CancelCommittedResultStatus.InvalidState, "Cannot cancel a reservation that is still in Reserved status. Use ReleaseReservation instead.");
        }

        if (currentReservation.Status == StockReservationStatus.Released)
        {
            return new CancelCommittedReservationResult(CancelCommittedResultStatus.InvalidState, "Cannot cancel a released reservation.");
        }

        return new CancelCommittedReservationResult(CancelCommittedResultStatus.InvalidState, "Reservation state conflict.");
    }
}
