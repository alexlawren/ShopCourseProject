using Grpc.Core;
using Shop.CatalogService.Application.StockReservation.Common;
using Shop.CatalogService.Application.StockReservation.Models;
using Shop.CatalogService.Application.StockReservation.Services;

namespace Shop.CatalogService.Grpc.Services;

public sealed class StockReservationGrpcService : StockReservationService.StockReservationServiceBase
{
    private readonly IStockReservationService _stockReservationService;

    public StockReservationGrpcService(IStockReservationService stockReservationService)
    {
        _stockReservationService = stockReservationService;
    }

    public override async Task<ReserveStockResponse> ReserveStock(
        ReserveStockRequest request,
        ServerCallContext context)
    {
        var validation = StockReservationRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, validation.ErrorMessage ?? "Invalid request."));
        }

        var requestId = Guid.Parse(request.RequestId);
        var parsedItems = request.Items
            .Select(i => (Guid.Parse(i.ProductId), i.Quantity))
            .ToList();

        var result = await _stockReservationService.ReserveStockAsync(requestId, parsedItems, context.CancellationToken);

        var response = new ReserveStockResponse
        {
            Success = result.IsSuccess,
            ReservationId = result.ReservationId?.ToString() ?? string.Empty,
            ErrorCode = result.ErrorCode ?? string.Empty,
            ErrorMessage = result.ErrorMessage ?? string.Empty
        };

        if (result.Items.Count > 0)
        {
            foreach (var item in result.Items)
            {
                response.Items.Add(new ReservedStockItem
                {
                    ProductId = item.ProductId.ToString(),
                    ProductName = item.ProductName,
                    UnitPriceMinor = PriceConverter.ToMinorUnits(item.UnitPrice),
                    Quantity = item.Quantity
                });
            }
        }

        return response;
    }

    public override async Task<ReleaseReservationResponse> ReleaseReservation(
        ReleaseReservationRequest request,
        ServerCallContext context)
    {
        var validation = StockReservationRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, validation.ErrorMessage ?? "Invalid request."));
        }

        var reservationId = Guid.Parse(request.ReservationId);
        var result = await _stockReservationService.ReleaseReservationAsync(reservationId, context.CancellationToken);

        return result.Status switch
        {
            ReleaseResultStatus.NotFound =>
                throw new RpcException(new Status(StatusCode.NotFound, result.Message ?? "Reservation not found.")),
            ReleaseResultStatus.AlreadyCommitted =>
                throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Message ?? "Cannot release a committed reservation.")),
            ReleaseResultStatus.Success =>
                new ReleaseReservationResponse { Success = true, Message = result.Message ?? "Reservation released successfully." },
            _ => throw new RpcException(new Status(StatusCode.Internal, "Unexpected release status."))
        };
    }

    public override async Task<CommitReservationResponse> CommitReservation(
        CommitReservationRequest request,
        ServerCallContext context)
    {
        var validation = StockReservationRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, validation.ErrorMessage ?? "Invalid request."));
        }

        var reservationId = Guid.Parse(request.ReservationId);
        var result = await _stockReservationService.CommitReservationAsync(reservationId, context.CancellationToken);

        return result.Status switch
        {
            CommitResultStatus.NotFound =>
                throw new RpcException(new Status(StatusCode.NotFound, result.Message ?? "Reservation not found.")),
            CommitResultStatus.AlreadyReleased =>
                throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Message ?? "Cannot commit a released reservation.")),
            CommitResultStatus.Success =>
                new CommitReservationResponse { Success = true, Message = result.Message ?? "Reservation committed successfully." },
            _ => throw new RpcException(new Status(StatusCode.Internal, "Unexpected commit status."))
        };
    }
}
