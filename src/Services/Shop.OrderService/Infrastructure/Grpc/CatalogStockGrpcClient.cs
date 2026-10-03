using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shop.CatalogService.Grpc;
using Shop.OrderService.Application.Orders.Common;
using Shop.OrderService.Application.Orders.Services;

namespace Shop.OrderService.Infrastructure.Grpc;

public sealed class CatalogStockGrpcClient : ICatalogStockClient
{
    private readonly StockReservationService.StockReservationServiceClient _grpcClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CatalogStockGrpcClient> _logger;

    public CatalogStockGrpcClient(
        StockReservationService.StockReservationServiceClient grpcClient,
        IConfiguration configuration,
        ILogger<CatalogStockGrpcClient> logger)
    {
        _grpcClient = grpcClient;
        _configuration = configuration;
        _logger = logger;
    }

    private DateTime GetDeadline()
    {
        var timeoutSeconds = _configuration.GetValue<int>("CatalogGrpc:TimeoutSeconds", 5);
        if (timeoutSeconds <= 0)
        {
            timeoutSeconds = 5;
        }

        return DateTime.UtcNow.AddSeconds(timeoutSeconds);
    }

    public async Task<CatalogReserveResult> ReserveStockAsync(
        Guid requestId,
        IReadOnlyList<CatalogReserveItem> items,
        CancellationToken cancellationToken = default)
    {
        var request = new ReserveStockRequest
        {
            RequestId = requestId.ToString()
        };

        foreach (var item in items)
        {
            request.Items.Add(new ReserveStockItem
            {
                ProductId = item.ProductId.ToString(),
                Quantity = item.Quantity
            });
        }

        try
        {
            var response = await _grpcClient.ReserveStockAsync(
                request,
                deadline: GetDeadline(),
                cancellationToken: cancellationToken);

            if (!response.Success)
            {
                return CatalogReserveResult.BusinessError(
                    response.ErrorCode ?? "RESERVATION_FAILED",
                    response.ErrorMessage ?? "Stock reservation failed.");
            }

            var validation = ReserveResponseValidator.Validate(response, items);
            if (!validation.IsValid)
            {
                _logger.LogError("Catalog ReserveStock response validation failed: {Error}", validation.ErrorMessage);

                if (validation.ReservationId != Guid.Empty)
                {
                    try
                    {
                        await ReleaseReservationAsync(validation.ReservationId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to compensate reservation {ReservationId} after validation error.", validation.ReservationId);
                    }
                }

                return CatalogReserveResult.DownstreamError(validation.ErrorMessage ?? "Invalid downstream response.");
            }

            return CatalogReserveResult.Success(validation.ReservationId, validation.Items);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "Catalog service unavailable or deadline exceeded for ReserveStock (RequestId: {RequestId}).", requestId);
            return CatalogReserveResult.Unavailable("Catalog service is currently unavailable or timed out.");
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Catalog service RpcException for ReserveStock (RequestId: {RequestId}, Code: {Code}, Detail: {Detail}).",
                requestId, ex.StatusCode, ex.Status.Detail);
            return CatalogReserveResult.DownstreamError($"Catalog service communication error: {ex.Status.Detail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during ReserveStock (RequestId: {RequestId}).", requestId);
            return CatalogReserveResult.DownstreamError("Downstream network communication failure.");
        }
    }

    public async Task<CatalogReleaseResult> ReleaseReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var request = new ReleaseReservationRequest
        {
            ReservationId = reservationId.ToString()
        };

        try
        {
            var response = await _grpcClient.ReleaseReservationAsync(
                request,
                deadline: GetDeadline(),
                cancellationToken: cancellationToken);

            return CatalogReleaseResult.Success(response.Message ?? "Reservation released.");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _logger.LogWarning("Catalog reservation {ReservationId} not found during Release.", reservationId);
            return CatalogReleaseResult.NotFound(ex.Status.Detail);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            _logger.LogWarning("Catalog reservation {ReservationId} already committed during Release: {Detail}", reservationId, ex.Status.Detail);
            return CatalogReleaseResult.AlreadyCommitted(ex.Status.Detail);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "Catalog service unavailable or deadline exceeded during Release (ReservationId: {ReservationId}).", reservationId);
            return CatalogReleaseResult.Unavailable("Catalog service is currently unavailable or timed out.");
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Catalog service RpcException during Release (ReservationId: {ReservationId}).", reservationId);
            return CatalogReleaseResult.DownstreamError($"Catalog service communication error: {ex.Status.Detail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Release (ReservationId: {ReservationId}).", reservationId);
            return CatalogReleaseResult.DownstreamError("Downstream network communication failure.");
        }
    }

    public async Task<CatalogCommitResult> CommitReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var request = new CommitReservationRequest
        {
            ReservationId = reservationId.ToString()
        };

        try
        {
            var response = await _grpcClient.CommitReservationAsync(
                request,
                deadline: GetDeadline(),
                cancellationToken: cancellationToken);

            return CatalogCommitResult.Success(response.Message ?? "Reservation committed.");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _logger.LogWarning("Catalog reservation {ReservationId} not found during Commit.", reservationId);
            return CatalogCommitResult.NotFound(ex.Status.Detail);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            _logger.LogWarning("Catalog reservation {ReservationId} already released during Commit: {Detail}", reservationId, ex.Status.Detail);
            return CatalogCommitResult.AlreadyReleased(ex.Status.Detail);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "Catalog service unavailable or deadline exceeded during Commit (ReservationId: {ReservationId}).", reservationId);
            return CatalogCommitResult.Unavailable("Catalog service is currently unavailable or timed out.");
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Catalog service RpcException during Commit (ReservationId: {ReservationId}).", reservationId);
            return CatalogCommitResult.DownstreamError($"Catalog service communication error: {ex.Status.Detail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Commit (ReservationId: {ReservationId}).", reservationId);
            return CatalogCommitResult.DownstreamError("Downstream network communication failure.");
        }
    }

    public async Task<CatalogCancelCommittedResult> CancelCommittedReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var request = new CancelCommittedReservationRequest
        {
            ReservationId = reservationId.ToString()
        };

        try
        {
            var response = await _grpcClient.CancelCommittedReservationAsync(
                request,
                deadline: GetDeadline(),
                cancellationToken: cancellationToken);

            return CatalogCancelCommittedResult.Success(response.Message ?? "Committed reservation cancelled.");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _logger.LogWarning("Catalog reservation {ReservationId} not found during CancelCommitted.", reservationId);
            return CatalogCancelCommittedResult.NotFound(ex.Status.Detail);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            _logger.LogWarning("Catalog reservation {ReservationId} invalid state during CancelCommitted: {Detail}", reservationId, ex.Status.Detail);
            return CatalogCancelCommittedResult.InvalidState(ex.Status.Detail);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "Catalog service unavailable or deadline exceeded during CancelCommitted (ReservationId: {ReservationId}).", reservationId);
            return CatalogCancelCommittedResult.Unavailable("Catalog service is currently unavailable or timed out.");
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Catalog service RpcException during CancelCommitted (ReservationId: {ReservationId}).", reservationId);
            return CatalogCancelCommittedResult.DownstreamError($"Catalog service communication error: {ex.Status.Detail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during CancelCommitted (ReservationId: {ReservationId}).", reservationId);
            return CatalogCancelCommittedResult.DownstreamError("Downstream network communication failure.");
        }
    }
}
