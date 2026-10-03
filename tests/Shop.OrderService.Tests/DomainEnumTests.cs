using Shop.OrderService.Domain.Enums;

namespace Shop.OrderService.Tests;

public class DomainEnumTests
{
    [Fact]
    public void OrderStatus_ContainsAllExpectedLifecycleStates()
    {
        var expectedNames = new[]
        {
            "Created",
            "Confirmed",
            "Processing",
            "Shipped",
            "Completed",
            "Cancelled"
        };

        var actualNames = Enum.GetNames<OrderStatus>();

        Assert.Equal(expectedNames.OrderBy(n => n), actualNames.OrderBy(n => n));
    }

    [Fact]
    public void PaymentStatus_ContainsAllExpectedLifecycleStates()
    {
        var expectedNames = new[]
        {
            "Pending",
            "Paid",
            "Cancelled"
        };

        var actualNames = Enum.GetNames<PaymentStatus>();

        Assert.Equal(expectedNames.OrderBy(n => n), actualNames.OrderBy(n => n));
    }

    [Fact]
    public void CancellationState_ContainsAllExpectedStates()
    {
        var expectedNames = new[]
        {
            "None",
            "Pending"
        };

        var actualNames = Enum.GetNames<CancellationState>();

        Assert.Equal(expectedNames.OrderBy(n => n), actualNames.OrderBy(n => n));
    }
}
