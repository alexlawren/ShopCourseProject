using Microsoft.EntityFrameworkCore;

namespace Shop.OrderService.Infrastructure.Persistence;

public sealed class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options)
        : base(options)
    {
    }
}
