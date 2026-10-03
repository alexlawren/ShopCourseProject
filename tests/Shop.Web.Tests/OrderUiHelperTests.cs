using Shop.Web.Services;

namespace Shop.Web.Tests;

public class OrderUiHelperTests
{
    [Theory]
    [InlineData("Created", true)]
    [InlineData("Confirmed", true)]
    [InlineData("Processing", true)]
    [InlineData("created", true)]
    [InlineData("confirmed", true)]
    [InlineData("processing", true)]
    [InlineData("Shipped", false)]
    [InlineData("Completed", false)]
    [InlineData("Cancelled", false)]
    [InlineData("shipped", false)]
    [InlineData("completed", false)]
    [InlineData("cancelled", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void CanCancel_EvaluatesStateProperly(string? status, bool expected)
    {
        var result = OrderUiHelper.CanCancel(status);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetStatusDisplayName_ReturnsRussianFriendlyNames()
    {
        Assert.Equal("Создан", OrderUiHelper.GetStatusDisplayName("Created"));
        Assert.Equal("Подтверждён", OrderUiHelper.GetStatusDisplayName("Confirmed"));
        Assert.Equal("В обработке", OrderUiHelper.GetStatusDisplayName("Processing"));
        Assert.Equal("Отправлен", OrderUiHelper.GetStatusDisplayName("Shipped"));
        Assert.Equal("Выполнен", OrderUiHelper.GetStatusDisplayName("Completed"));
        Assert.Equal("Отменён", OrderUiHelper.GetStatusDisplayName("Cancelled"));
    }

    [Fact]
    public void GetPaymentStatusDisplayName_ReturnsRussianFriendlyNames()
    {
        Assert.Equal("Ожидает оплаты", OrderUiHelper.GetPaymentStatusDisplayName("Pending"));
        Assert.Equal("Оплачен", OrderUiHelper.GetPaymentStatusDisplayName("Paid"));
        Assert.Equal("Отменён", OrderUiHelper.GetPaymentStatusDisplayName("Cancelled"));
    }
}
