using Microsoft.EntityFrameworkCore;
using Shop.OrderService.Application.Orders.Dtos;
using Shop.OrderService.Infrastructure.Persistence;

namespace Shop.OrderService.Application.Orders.Services;

public sealed class OrderQueryService : IOrderQueryService
{
    private readonly OrderDbContext _dbContext;

    public OrderQueryService(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<OrderListItemDto>> GetOrdersAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderListItemDto
            {
                Id = o.Id,
                Status = o.Status.ToString(),
                PaymentStatus = o.PaymentStatus.ToString(),
                TotalAmount = o.TotalAmount,
                CreatedAtUtc = o.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<OrderDetailsDto?> GetOrderByIdAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null || order.UserId != userId)
        {
            // Do NOT return 403 to prevent ID probing - return null so controller returns 404
            return null;
        }

        return CheckoutService.MapToDetailsDto(order);
    }
}
