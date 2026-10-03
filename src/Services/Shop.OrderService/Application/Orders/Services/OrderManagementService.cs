using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shop.OrderService.Application.Orders.Common;
using Shop.OrderService.Application.Orders.Dtos;
using Shop.OrderService.Application.Orders.Notifications;
using Shop.OrderService.Domain.Entities;
using Shop.OrderService.Domain.Enums;
using Shop.OrderService.Infrastructure.Persistence;

namespace Shop.OrderService.Application.Orders.Services;

public sealed class OrderManagementService : IOrderManagementService
{
    private readonly OrderDbContext _dbContext;
    private readonly ICatalogStockClient _catalogStockClient;
    private readonly IOrderNotificationService _notificationService;
    private readonly ILogger<OrderManagementService> _logger;

    public OrderManagementService(
        OrderDbContext dbContext,
        ICatalogStockClient catalogStockClient,
        IOrderNotificationService notificationService,
        ILogger<OrderManagementService> logger)
    {
        _dbContext = dbContext;
        _catalogStockClient = catalogStockClient;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<OrderOperationResult> PayOrderAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Orders\" WHERE \"Id\" = {orderId} FOR UPDATE",
            cancellationToken);

        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null || order.UserId != userId)
        {
            await tx.RollbackAsync(cancellationToken);
            return OrderOperationResult.NotFound();
        }

        if (order.CancellationState == CancellationState.Pending)
        {
            await tx.RollbackAsync(cancellationToken);
            return OrderOperationResult.CancellationInProgress("Order cancellation is in progress.");
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            // Idempotent: already paid, do not add duplicate history
            await tx.RollbackAsync(cancellationToken);
            return OrderOperationResult.Success(CheckoutService.MapToDetailsDto(order));
        }

        if (order.PaymentStatus == PaymentStatus.Cancelled || order.Status == OrderStatus.Cancelled)
        {
            await tx.RollbackAsync(cancellationToken);
            return OrderOperationResult.PaymentCancelled("Cannot pay for a cancelled order.");
        }

        var now = DateTime.UtcNow;
        order.PaymentStatus = PaymentStatus.Paid;
        order.UpdatedAtUtc = now;

        bool statusChangedToConfirmed = false;
        if (order.Status == OrderStatus.Created)
        {
            statusChangedToConfirmed = true;
            order.Status = OrderStatus.Confirmed;
            _dbContext.OrderStatusHistory.Add(new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Status = OrderStatus.Confirmed,
                ChangedAtUtc = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        await _notificationService.NotifyPaymentStatusChangedAsync(
            new PaymentStatusChangedEvent(order.Id, PaymentStatus.Paid.ToString(), now),
            order.UserId,
            cancellationToken);

        if (statusChangedToConfirmed)
        {
            await _notificationService.NotifyOrderStatusChangedAsync(
                new OrderStatusChangedEvent(order.Id, OrderStatus.Confirmed.ToString(), now),
                order.UserId,
                cancellationToken);
        }

        return OrderOperationResult.Success(CheckoutService.MapToDetailsDto(order));
    }

    public async Task<OrderOperationResult> CancelOrderAsync(
        Guid? userId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        // -------------------------------------------------------------
        // PHASE A: Durable Local Cancellation Intent
        // -------------------------------------------------------------
        Guid reservationId;

        await using (var txA = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken))
        {
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Orders\" WHERE \"Id\" = {orderId} FOR UPDATE",
                cancellationToken);

            var orderA = await _dbContext.Orders
                .Include(o => o.Items)
                .Include(o => o.StatusHistory)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (orderA == null || (userId.HasValue && orderA.UserId != userId.Value))
            {
                await txA.RollbackAsync(cancellationToken);
                return OrderOperationResult.NotFound();
            }

            if (orderA.Status == OrderStatus.Cancelled)
            {
                // Idempotent cancel: already cancelled, stock was already returned
                await txA.RollbackAsync(cancellationToken);
                return OrderOperationResult.Success(
                    CheckoutService.MapToDetailsDto(orderA),
                    CheckoutService.MapToAdminDetailsDto(orderA));
            }

            if (orderA.CancellationState != CancellationState.Pending && !OrderStateMachine.CanCancel(orderA.Status))
            {
                await txA.RollbackAsync(cancellationToken);
                return OrderOperationResult.CannotCancel(
                    $"Order in status '{orderA.Status}' cannot be cancelled.");
            }

            if (orderA.CancellationState != CancellationState.Pending)
            {
                orderA.CancellationState = CancellationState.Pending;
                orderA.UpdatedAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await txA.CommitAsync(cancellationToken);
            reservationId = orderA.ReservationId;
        }

        // -------------------------------------------------------------
        // PHASE B: External Side-Effect (Catalog committed stock cancellation)
        // -------------------------------------------------------------
        var catalogResult = await _catalogStockClient.CancelCommittedReservationAsync(reservationId, cancellationToken);

        if (catalogResult.Status == CatalogCancelCommittedStatus.Unavailable)
        {
            _logger.LogWarning("Catalog service unavailable during CancelCommittedReservation for Order {OrderId}.", orderId);
            return OrderOperationResult.CatalogUnavailable("Catalog service is currently unavailable. Please retry cancellation.");
        }

        if (!catalogResult.IsSuccess)
        {
            _logger.LogError("Failed to cancel committed reservation {ReservationId} for Order {OrderId}: {Status} - {Message}",
                reservationId, orderId, catalogResult.Status, catalogResult.Message);
            return OrderOperationResult.DownstreamError(catalogResult.Message ?? "Failed to cancel catalog reservation.");
        }

        // -------------------------------------------------------------
        // PHASE C: Local Finalization
        // -------------------------------------------------------------
        await using var txC = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Orders\" WHERE \"Id\" = {orderId} FOR UPDATE",
            cancellationToken);

        var orderC = await _dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (orderC == null)
        {
            await txC.RollbackAsync(cancellationToken);
            return OrderOperationResult.NotFound();
        }

        if (orderC.Status == OrderStatus.Cancelled)
        {
            // Another concurrent call finalized it
            await txC.RollbackAsync(cancellationToken);
            return OrderOperationResult.Success(
                CheckoutService.MapToDetailsDto(orderC),
                CheckoutService.MapToAdminDetailsDto(orderC));
        }

        var now = DateTime.UtcNow;
        bool paymentStatusChanged = orderC.PaymentStatus != PaymentStatus.Cancelled;

        orderC.Status = OrderStatus.Cancelled;
        orderC.PaymentStatus = PaymentStatus.Cancelled;
        orderC.CancellationState = CancellationState.None;
        orderC.UpdatedAtUtc = now;

        _dbContext.OrderStatusHistory.Add(new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = orderC.Id,
            Status = OrderStatus.Cancelled,
            ChangedAtUtc = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await txC.CommitAsync(cancellationToken);

        await _notificationService.NotifyOrderStatusChangedAsync(
            new OrderStatusChangedEvent(orderC.Id, OrderStatus.Cancelled.ToString(), now),
            orderC.UserId,
            cancellationToken);

        if (paymentStatusChanged)
        {
            await _notificationService.NotifyPaymentStatusChangedAsync(
                new PaymentStatusChangedEvent(orderC.Id, PaymentStatus.Cancelled.ToString(), now),
                orderC.UserId,
                cancellationToken);
        }

        return OrderOperationResult.Success(
            CheckoutService.MapToDetailsDto(orderC),
            CheckoutService.MapToAdminDetailsDto(orderC));
    }

    public async Task<OrderOperationResult> AdminUpdateStatusAsync(
        Guid orderId,
        OrderStatus nextStatus,
        CancellationToken cancellationToken = default)
    {
        if (nextStatus == OrderStatus.Cancelled)
        {
            return OrderOperationResult.InvalidTransition(
                "Status 'Cancelled' cannot be set via this endpoint. Use the cancel endpoint instead.");
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Orders\" WHERE \"Id\" = {orderId} FOR UPDATE",
            cancellationToken);

        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            await tx.RollbackAsync(cancellationToken);
            return OrderOperationResult.NotFound();
        }

        if (order.CancellationState == CancellationState.Pending)
        {
            await tx.RollbackAsync(cancellationToken);
            return OrderOperationResult.CancellationInProgress("Order cancellation is in progress.");
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            await tx.RollbackAsync(cancellationToken);
            return OrderOperationResult.InvalidTransition("Cannot change status of a cancelled order.");
        }

        if (order.Status == nextStatus)
        {
            // Idempotent: already at target status
            await tx.RollbackAsync(cancellationToken);
            return OrderOperationResult.AdminSuccess(CheckoutService.MapToAdminDetailsDto(order));
        }

        if (!OrderStateMachine.CanAdminTransition(order.Status, nextStatus))
        {
            await tx.RollbackAsync(cancellationToken);
            return OrderOperationResult.InvalidTransition(
                $"Cannot transition order from '{order.Status}' to '{nextStatus}'.");
        }

        var now = DateTime.UtcNow;
        order.Status = nextStatus;
        order.UpdatedAtUtc = now;

        _dbContext.OrderStatusHistory.Add(new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Status = nextStatus,
            ChangedAtUtc = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        await _notificationService.NotifyOrderStatusChangedAsync(
            new OrderStatusChangedEvent(order.Id, nextStatus.ToString(), now),
            order.UserId,
            cancellationToken);

        return OrderOperationResult.AdminSuccess(CheckoutService.MapToAdminDetailsDto(order));
    }

    public async Task<PagedResult<AdminOrderListItemDto>> AdminGetOrdersAsync(
        int page,
        int pageSize,
        OrderStatus? status,
        PaymentStatus? paymentStatus,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.Orders.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (paymentStatus.HasValue)
        {
            query = query.Where(o => o.PaymentStatus == paymentStatus.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new AdminOrderListItemDto
            {
                Id = o.Id,
                UserId = o.UserId,
                Status = o.Status.ToString(),
                PaymentStatus = o.PaymentStatus.ToString(),
                TotalAmount = o.TotalAmount,
                CreatedAtUtc = o.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminOrderListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<AdminOrderDetailsDto?> AdminGetOrderByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            return null;
        }

        return CheckoutService.MapToAdminDetailsDto(order);
    }
}
