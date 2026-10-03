namespace Shop.Web.Services;

public static class CheckoutIdempotencyHelper
{
    /// <summary>
    /// Determines whether the pending checkout RequestId should be cleared after a failed checkout attempt.
    /// Returns true for deterministic business failures (where the cart must be fixed and a new checkout started),
    /// and false for uncertain errors (502, 503, network errors) where retry must preserve the same RequestId.
    /// </summary>
    public static bool ShouldClearRequestIdOnFailure(int statusCode, string? errorCode)
    {
        // 503 Service Unavailable / CATALOG_UNAVAILABLE -> Uncertain, PRESERVE for retry
        if (statusCode == 503 || string.Equals(errorCode, "CATALOG_UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 502 Bad Gateway / DOWNSTREAM_ERROR -> Uncertain, PRESERVE for retry
        if (statusCode == 502 || string.Equals(errorCode, "DOWNSTREAM_ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Network interruption / timeout (represented as statusCode 0) -> Uncertain, PRESERVE for retry
        if (statusCode == 0)
        {
            return false;
        }

        // Deterministic business failures BEFORE order creation:
        // EMPTY_CART, OUT_OF_STOCK, PRODUCT_NOT_FOUND, PRODUCT_INACTIVE, CATEGORY_INACTIVE, CART_CHANGED, INVALID_REQUEST_ID, CART_TOO_LARGE
        if (string.Equals(errorCode, "EMPTY_CART", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(errorCode, "OUT_OF_STOCK", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(errorCode, "PRODUCT_NOT_FOUND", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(errorCode, "PRODUCT_INACTIVE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(errorCode, "CATEGORY_INACTIVE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(errorCode, "CART_CHANGED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(errorCode, "INVALID_REQUEST_ID", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(errorCode, "CART_TOO_LARGE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Client errors (4xx) are deterministic and should clear the pending request ID
        if (statusCode is >= 400 and < 500)
        {
            return true;
        }

        // Other 5xx errors represent downstream / server uncertainties -> preserve
        return false;
    }
}
