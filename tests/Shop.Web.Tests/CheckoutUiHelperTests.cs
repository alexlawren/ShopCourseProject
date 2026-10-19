using Shop.Web.Models.Auth;
using Shop.Web.Services;

namespace Shop.Web.Tests;

public class CheckoutUiHelperTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(502)]
    [InlineData(503)]
    public void CanSubmitCheckout_RetryAfterServerClearedCart_UsesPendingRequest(int statusCode)
    {
        var failure = CheckoutUiHelper.ProcessCheckoutFailure(statusCode, null, null);

        Assert.True(CheckoutUiHelper.CanSubmitCheckout(false, failure.CanRetry, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void CanSubmitCheckout_EmptyCartWithoutPendingRequest_DoesNotCreateOrder(bool hasValidCart, bool canRetry)
    {
        Assert.False(CheckoutUiHelper.CanSubmitCheckout(hasValidCart, canRetry, null));
        Assert.False(CheckoutUiHelper.CanSubmitCheckout(hasValidCart, canRetry, Guid.Empty));
    }

    [Fact]
    public void CanSubmitCheckout_NewOrderRequiresValidCart()
    {
        Assert.True(CheckoutUiHelper.CanSubmitCheckout(true, false, null));
        Assert.False(CheckoutUiHelper.CanSubmitCheckout(false, false, Guid.NewGuid()));
    }

    [Theory]
    [InlineData("OUT_OF_STOCK", 409, "Недостаточно товара на складе.", true, false)]
    [InlineData("EMPTY_CART", 400, "Корзина пуста. Добавьте товары перед оформлением заказа.", true, false)]
    [InlineData("CART_CHANGED", 409, "Содержимое корзины изменилось во время оформления. Пожалуйста, проверьте корзину.", true, false)]
    [InlineData("PRODUCT_NOT_FOUND", 409, "Один или несколько товаров не найдены.", true, false)]
    [InlineData("PRODUCT_INACTIVE", 409, "Один из товаров в корзине снят с продажи.", true, false)]
    [InlineData("CATEGORY_INACTIVE", 409, "Категория одного из товаров в корзине неактивна.", true, false)]
    [InlineData("CATALOG_UNAVAILABLE", 503, "Сервис каталога временно недоступен. Пожалуйста, повторите попытку.", false, true)]
    public void ProcessCheckoutFailure_StandardCheckoutErrorCodes_ProducesExpectedUiState(
        string errorCode,
        int statusCode,
        string expectedDisplayMessage,
        bool expectedShouldClear,
        bool expectedCanRetry)
    {
        // 1. ProblemDetails mapping resolves to friendly message
        var friendlyMessage = ProblemDetailsModel.MapCodeToMessage(errorCode);
        Assert.Equal(expectedDisplayMessage, friendlyMessage);

        // 2. CheckoutUiHelper computes UI state
        var failureState = CheckoutUiHelper.ProcessCheckoutFailure(statusCode, errorCode, friendlyMessage);

        Assert.Equal(expectedDisplayMessage, failureState.ErrorMessage);
        Assert.Equal(expectedShouldClear, failureState.ShouldClearRequestId);
        Assert.Equal(expectedCanRetry, failureState.CanRetry);
    }

    [Fact]
    public void ProcessCheckoutFailure_OutOfStockScenario_ClearsRequestIdAndDisablesAlertRetry()
    {
        // Scenario from user: stock=2, cart=3, POST /api/orders returns 409 Conflict OUT_OF_STOCK
        var mappedMessage = ProblemDetailsModel.MapCodeToMessage("OUT_OF_STOCK");
        var state = CheckoutUiHelper.ProcessCheckoutFailure(409, "OUT_OF_STOCK", mappedMessage);

        Assert.Equal("Недостаточно товара на складе.", state.ErrorMessage);
        Assert.True(state.ShouldClearRequestId, "Deterministic failure MUST clear pending RequestId");
        Assert.False(state.CanRetry, "Deterministic failure must NOT show alert retry button with stale RequestId");
    }

    [Fact]
    public void ProcessCheckoutFailure_NullOrEmptyErrorMessage_FallsBackToDefaultRussianMessage()
    {
        var stateNull = CheckoutUiHelper.ProcessCheckoutFailure(409, "OUT_OF_STOCK", null);
        Assert.Equal("Произошла ошибка при оформлении заказа.", stateNull.ErrorMessage);

        var stateEmpty = CheckoutUiHelper.ProcessCheckoutFailure(409, "OUT_OF_STOCK", "   ");
        Assert.Equal("Произошла ошибка при оформлении заказа.", stateEmpty.ErrorMessage);
    }

    [Fact]
    public void ProcessCheckoutFailure_DownstreamOrNetworkError_PreservesRequestIdForRetry()
    {
        var netState = CheckoutUiHelper.ProcessCheckoutFailure(0, null, "Ошибка сети: Failed to fetch");
        Assert.False(netState.ShouldClearRequestId);
        Assert.True(netState.CanRetry);

        var gatewayState = CheckoutUiHelper.ProcessCheckoutFailure(502, "DOWNSTREAM_ERROR", "Сервис временно недоступен");
        Assert.False(gatewayState.ShouldClearRequestId);
        Assert.True(gatewayState.CanRetry);
    }
}
