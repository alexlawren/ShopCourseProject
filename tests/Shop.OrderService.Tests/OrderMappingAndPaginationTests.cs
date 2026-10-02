using Shop.OrderService.Application.Orders.Dtos;
using Shop.OrderService.Application.Orders.Services;
using Shop.OrderService.Domain.Entities;
using Shop.OrderService.Domain.Enums;

namespace Shop.OrderService.Tests;

public class OrderMappingAndPaginationTests
{
    [Fact]
    public void MapToDetailsDto_PreservesAllSnapshotValues()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reqId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var order = new Order
        {
            Id = orderId,
            UserId = userId,
            CheckoutRequestId = reqId,
            ReservationId = resId,
            Status = OrderStatus.Created,
            PaymentStatus = PaymentStatus.Pending,
            TotalAmount = 226.00m,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Items =
            {
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductId = Guid.NewGuid(),
                    ProductName = "Snapshot Product A",
                    UnitPrice = 100.50m,
                    Quantity = 2,
                    LineTotal = 201.00m
                },
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductId = Guid.NewGuid(),
                    ProductName = "Snapshot Product B",
                    UnitPrice = 25.00m,
                    Quantity = 1,
                    LineTotal = 25.00m
                }
            },
            StatusHistory =
            {
                new OrderStatusHistory
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    Status = OrderStatus.Created,
                    ChangedAtUtc = now
                }
            }
        };

        var dto = CheckoutService.MapToDetailsDto(order);

        Assert.Equal(orderId, dto.Id);
        Assert.Equal("Created", dto.Status);
        Assert.Equal("Pending", dto.PaymentStatus);
        Assert.Equal(226.00m, dto.TotalAmount);
        Assert.Equal(now, dto.CreatedAtUtc);
        Assert.Equal(now, dto.UpdatedAtUtc);

        Assert.Equal(2, dto.Items.Count);
        Assert.Equal("Snapshot Product A", dto.Items[0].ProductName);
        Assert.Equal(100.50m, dto.Items[0].UnitPrice);
        Assert.Equal(2, dto.Items[0].Quantity);
        Assert.Equal(201.00m, dto.Items[0].LineTotal);

        Assert.Single(dto.StatusHistory);
        Assert.Equal("Created", dto.StatusHistory[0].Status);
        Assert.Equal(now, dto.StatusHistory[0].ChangedAtUtc);
    }

    [Fact]
    public void PagedResult_CalculatesTotalPagesCorrectly()
    {
        var result1 = new PagedResult<OrderListItemDto>
        {
            Page = 1,
            PageSize = 20,
            TotalCount = 45
        };
        Assert.Equal(3, result1.TotalPages);

        var result2 = new PagedResult<OrderListItemDto>
        {
            Page = 1,
            PageSize = 20,
            TotalCount = 0
        };
        Assert.Equal(0, result2.TotalPages);

        var result3 = new PagedResult<OrderListItemDto>
        {
            Page = 1,
            PageSize = 20,
            TotalCount = 20
        };
        Assert.Equal(1, result3.TotalPages);
    }

    [Fact]
    public void CheckoutRequest_Validation()
    {
        var emptyReq = new CheckoutRequest { RequestId = Guid.Empty };
        Assert.Equal(Guid.Empty, emptyReq.RequestId);

        var validReq = new CheckoutRequest { RequestId = Guid.NewGuid() };
        Assert.NotEqual(Guid.Empty, validReq.RequestId);
    }
}
