namespace Shop.Web.Services;

public record CheckoutFailureState(string ErrorMessage, bool ShouldClearRequestId, bool CanRetry);

public static class CheckoutUiHelper
{
    public static bool CanSubmitCheckout(bool hasValidCart, bool canRetry, Guid? pendingRequestId)
        => hasValidCart || (canRetry && pendingRequestId.HasValue && pendingRequestId.Value != Guid.Empty);

    /// <summary>
    /// Computes the UI state for checkout failure:
    /// Friendly error message to display, whether the pending request ID should be cleared,
    /// and whether the user can retry with the same request ID.
    /// </summary>
    public static CheckoutFailureState ProcessCheckoutFailure(int statusCode, string? errorCode, string? errorMessage)
    {
        var displayError = !string.IsNullOrWhiteSpace(errorMessage)
            ? errorMessage
            : "Произошла ошибка при оформлении заказа.";

        var shouldClear = CheckoutIdempotencyHelper.ShouldClearRequestIdOnFailure(statusCode, errorCode);
        var canRetry = !shouldClear;

        return new CheckoutFailureState(displayError, shouldClear, canRetry);
    }
}
