using Shop.CatalogService.Grpc;

namespace Shop.CatalogService.Application.StockReservation.Common;

public sealed class ValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    private ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult Success() => new(true, null);
    public static ValidationResult Fail(string message) => new(false, message);
}

public static class StockReservationRequestValidator
{
    public static ValidationResult Validate(ReserveStockRequest request)
    {
        if (request == null)
        {
            return ValidationResult.Fail("Request cannot be null.");
        }

        if (!Guid.TryParse(request.RequestId, out var requestId) || requestId == Guid.Empty)
        {
            return ValidationResult.Fail("Invalid or missing request_id GUID.");
        }

        if (request.Items.Count < 1 || request.Items.Count > 100)
        {
            return ValidationResult.Fail("Items count must be between 1 and 100.");
        }

        var seenProductIds = new HashSet<Guid>();

        foreach (var item in request.Items)
        {
            if (!Guid.TryParse(item.ProductId, out var productId) || productId == Guid.Empty)
            {
                return ValidationResult.Fail($"Invalid product_id GUID: '{item.ProductId}'.");
            }

            if (item.Quantity <= 0 || item.Quantity > 1000)
            {
                return ValidationResult.Fail($"Quantity for product '{productId}' must be between 1 and 1000. Got: {item.Quantity}.");
            }

            if (!seenProductIds.Add(productId))
            {
                return ValidationResult.Fail($"Duplicate product_id in request: '{productId}'.");
            }
        }

        return ValidationResult.Success();
    }

    public static ValidationResult Validate(ReleaseReservationRequest request)
    {
        if (request == null)
        {
            return ValidationResult.Fail("Request cannot be null.");
        }

        if (!Guid.TryParse(request.ReservationId, out var reservationId) || reservationId == Guid.Empty)
        {
            return ValidationResult.Fail("Invalid or missing reservation_id GUID.");
        }

        return ValidationResult.Success();
    }

    public static ValidationResult Validate(CommitReservationRequest request)
    {
        if (request == null)
        {
            return ValidationResult.Fail("Request cannot be null.");
        }

        if (!Guid.TryParse(request.ReservationId, out var reservationId) || reservationId == Guid.Empty)
        {
            return ValidationResult.Fail("Invalid or missing reservation_id GUID.");
        }

        return ValidationResult.Success();
    }

    public static ValidationResult Validate(CancelCommittedReservationRequest request)
    {
        if (request == null)
        {
            return ValidationResult.Fail("Request cannot be null.");
        }

        if (!Guid.TryParse(request.ReservationId, out var reservationId) || reservationId == Guid.Empty)
        {
            return ValidationResult.Fail("Invalid or missing reservation_id GUID.");
        }

        return ValidationResult.Success();
    }
}
