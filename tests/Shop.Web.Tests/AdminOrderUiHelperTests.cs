using Shop.Web.Services;

namespace Shop.Web.Tests;

public class AdminOrderUiHelperTests
{
    [Theory]
    [InlineData("Created", "Confirmed")]
    [InlineData("created", "Confirmed")]
    [InlineData("Confirmed", "Processing")]
    [InlineData("Processing", "Shipped")]
    [InlineData("Shipped", "Completed")]
    [InlineData("Completed", null)]
    [InlineData("Cancelled", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("Unknown", null)]
    public void GetNextAllowedStatus_ReturnsExpectedTransition(string? current, string? expectedNext)
    {
        var result = AdminOrderUiHelper.GetNextAllowedStatus(current);
        Assert.Equal(expectedNext, result);
    }

    [Theory]
    [InlineData("Created", true)]
    [InlineData("Confirmed", true)]
    [InlineData("Processing", true)]
    [InlineData("Shipped", false)]
    [InlineData("Completed", false)]
    [InlineData("Cancelled", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("Random", false)]
    public void CanAdminCancel_OnlyAllowedForCreatedConfirmedProcessing(string? status, bool expectedCanCancel)
    {
        var result = AdminOrderUiHelper.CanAdminCancel(status);
        Assert.Equal(expectedCanCancel, result);
    }

    [Theory]
    [InlineData("Created", "Подтвердить заказ (Confirmed)")]
    [InlineData("Confirmed", "Перевести в обработку (Processing)")]
    [InlineData("Processing", "Отправить заказ (Shipped)")]
    [InlineData("Shipped", "Завершить заказ (Completed)")]
    [InlineData("Completed", "")]
    [InlineData("Cancelled", "")]
    public void GetNextStatusActionName_ReturnsDescriptiveText(string status, string expectedText)
    {
        var result = AdminOrderUiHelper.GetNextStatusActionName(status);
        Assert.Equal(expectedText, result);
    }
}
