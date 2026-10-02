namespace Shop.OrderService.Application.Cart.Services;

public enum CartOperationStatus
{
    Success,
    NotFound,
    BadRequest
}

public sealed class CartResult<T>
{
    public CartOperationStatus Status { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }

    private CartResult(CartOperationStatus status, T? value, string? errorMessage)
    {
        Status = status;
        Value = value;
        ErrorMessage = errorMessage;
    }

    public static CartResult<T> Success(T value) => new(CartOperationStatus.Success, value, null);
    public static CartResult<T> NotFound(string message = "Entity not found.") => new(CartOperationStatus.NotFound, default, message);
    public static CartResult<T> BadRequest(string message) => new(CartOperationStatus.BadRequest, default, message);
}

public sealed class CartResult
{
    public CartOperationStatus Status { get; }
    public string? ErrorMessage { get; }

    private CartResult(CartOperationStatus status, string? errorMessage)
    {
        Status = status;
        ErrorMessage = errorMessage;
    }

    public static CartResult Success() => new(CartOperationStatus.Success, null);
    public static CartResult NotFound(string message = "Entity not found.") => new(CartOperationStatus.NotFound, message);
    public static CartResult BadRequest(string message) => new(CartOperationStatus.BadRequest, message);
}
