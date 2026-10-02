namespace Shop.OrderService.Application.Orders.Services;

public record CatalogReserveItem(Guid ProductId, int Quantity);

public record CatalogReservedItem(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public enum CatalogReserveStatus
{
    Success,
    BusinessError,
    Unavailable,
    DownstreamError
}

public sealed class CatalogReserveResult
{
    public CatalogReserveStatus Status { get; init; }
    public bool IsSuccess => Status == CatalogReserveStatus.Success;
    public Guid ReservationId { get; init; }
    public IReadOnlyList<CatalogReservedItem> Items { get; init; } = Array.Empty<CatalogReservedItem>();
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static CatalogReserveResult Success(Guid reservationId, IReadOnlyList<CatalogReservedItem> items) =>
        new() { Status = CatalogReserveStatus.Success, ReservationId = reservationId, Items = items };

    public static CatalogReserveResult BusinessError(string code, string message) =>
        new() { Status = CatalogReserveStatus.BusinessError, ErrorCode = code, ErrorMessage = message };

    public static CatalogReserveResult Unavailable(string message) =>
        new() { Status = CatalogReserveStatus.Unavailable, ErrorCode = "CATALOG_UNAVAILABLE", ErrorMessage = message };

    public static CatalogReserveResult DownstreamError(string message) =>
        new() { Status = CatalogReserveStatus.DownstreamError, ErrorCode = "DOWNSTREAM_ERROR", ErrorMessage = message };
}

public enum CatalogReleaseStatus
{
    Success,
    NotFound,
    AlreadyCommitted,
    Unavailable,
    DownstreamError
}

public sealed class CatalogReleaseResult
{
    public CatalogReleaseStatus Status { get; init; }
    public bool IsSuccess => Status == CatalogReleaseStatus.Success;
    public string? Message { get; init; }

    public static CatalogReleaseResult Success(string message) =>
        new() { Status = CatalogReleaseStatus.Success, Message = message };

    public static CatalogReleaseResult NotFound(string message) =>
        new() { Status = CatalogReleaseStatus.NotFound, Message = message };

    public static CatalogReleaseResult AlreadyCommitted(string message) =>
        new() { Status = CatalogReleaseStatus.AlreadyCommitted, Message = message };

    public static CatalogReleaseResult Unavailable(string message) =>
        new() { Status = CatalogReleaseStatus.Unavailable, Message = message };

    public static CatalogReleaseResult DownstreamError(string message) =>
        new() { Status = CatalogReleaseStatus.DownstreamError, Message = message };
}

public enum CatalogCommitStatus
{
    Success,
    NotFound,
    AlreadyReleased,
    Unavailable,
    DownstreamError
}

public sealed class CatalogCommitResult
{
    public CatalogCommitStatus Status { get; init; }
    public bool IsSuccess => Status == CatalogCommitStatus.Success;
    public string? Message { get; init; }

    public static CatalogCommitResult Success(string message) =>
        new() { Status = CatalogCommitStatus.Success, Message = message };

    public static CatalogCommitResult NotFound(string message) =>
        new() { Status = CatalogCommitStatus.NotFound, Message = message };

    public static CatalogCommitResult AlreadyReleased(string message) =>
        new() { Status = CatalogCommitStatus.AlreadyReleased, Message = message };

    public static CatalogCommitResult Unavailable(string message) =>
        new() { Status = CatalogCommitStatus.Unavailable, Message = message };

    public static CatalogCommitResult DownstreamError(string message) =>
        new() { Status = CatalogCommitStatus.DownstreamError, Message = message };
}

public interface ICatalogStockClient
{
    Task<CatalogReserveResult> ReserveStockAsync(
        Guid requestId,
        IReadOnlyList<CatalogReserveItem> items,
        CancellationToken cancellationToken = default);

    Task<CatalogReleaseResult> ReleaseReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<CatalogCommitResult> CommitReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);
}
