using System.Text.Json;
using Shop.Web.Models.Auth;

namespace Shop.Web.Tests;

public class ProblemDetailsModelTests
{
    [Theory]
    [InlineData("EMPTY_CART", "Корзина пуста. Добавьте товары перед оформлением заказа.")]
    [InlineData("CART_CHANGED", "Содержимое корзины изменилось во время оформления. Пожалуйста, проверьте корзину.")]
    [InlineData("OUT_OF_STOCK", "Недостаточно товара на складе для оформления заказа.")]
    [InlineData("PRODUCT_NOT_FOUND", "Один или несколько товаров не найдены.")]
    [InlineData("PRODUCT_INACTIVE", "Один из товаров в корзине снят с продажи.")]
    [InlineData("CATEGORY_INACTIVE", "Категория одного из товаров в корзине неактивна.")]
    [InlineData("CATALOG_UNAVAILABLE", "Сервис каталога временно недоступен. Пожалуйста, повторите попытку.")]
    [InlineData("ORDER_CANCELLATION_IN_PROGRESS", "Отмена заказа уже выполняется. Повторите через несколько секунд.")]
    [InlineData("PAYMENT_CANCELLED", "Заказ отменен, оплата невозможна.")]
    [InlineData("ORDER_CANNOT_BE_CANCELLED", "Заказ не может быть отменен на текущей стадии.")]
    [InlineData("INVALID_ORDER_TRANSITION", "Недопустимое изменение статуса заказа.")]
    [InlineData("DOWNSTREAM_ERROR", "Внутренняя ошибка сервиса при обращении к каталогу. Пожалуйста, повторите попытку.")]
    public void GetDisplayMessage_KnownCode_ReturnsRussianFriendlyMessage(string code, string expectedMessage)
    {
        var model = new ProblemDetailsModel { Code = code };
        Assert.Equal(expectedMessage, model.GetDisplayMessage());
    }

    [Fact]
    public void GetDisplayMessage_CodeFromExtensionData_DeserializedAndMapped()
    {
        var json = """
        {
            "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            "title": "Conflict",
            "status": 409,
            "detail": "Some technical internal error",
            "code": "OUT_OF_STOCK"
        }
        """;

        var model = JsonSerializer.Deserialize<ProblemDetailsModel>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(model);
        Assert.Equal("OUT_OF_STOCK", model.GetErrorCode());
        Assert.Equal("Недостаточно товара на складе для оформления заказа.", model.GetDisplayMessage());
    }

    [Fact]
    public void GetDisplayMessage_NoCode_FallsBackToDetailOrErrorsOrTitle()
    {
        var modelWithDetail = new ProblemDetailsModel { Detail = "Custom detail" };
        Assert.Equal("Custom detail", modelWithDetail.GetDisplayMessage());

        var modelWithErrors = new ProblemDetailsModel
        {
            Errors = new Dictionary<string, string[]>
            {
                { "Quantity", ["Quantity must be between 1 and 1000."] }
            }
        };
        Assert.Equal("Quantity must be between 1 and 1000.", modelWithErrors.GetDisplayMessage());

        var modelWithTitle = new ProblemDetailsModel { Title = "Bad Request" };
        Assert.Equal("Bad Request", modelWithTitle.GetDisplayMessage());
    }
}
