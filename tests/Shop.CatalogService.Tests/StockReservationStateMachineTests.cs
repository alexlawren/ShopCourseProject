using Shop.CatalogService.Application.StockReservation.Common;
using Shop.CatalogService.Domain.Enums;

namespace Shop.CatalogService.Tests;

public class StockReservationStateMachineTests
{
    [Fact]
    public void CanCommit_WhenReserved_ReturnsTrue()
    {
        Assert.True(StockReservationStateMachine.CanCommit(StockReservationStatus.Reserved));
    }

    [Fact]
    public void CanCommit_WhenCommitted_ReturnsTrue_ForIdempotency()
    {
        Assert.True(StockReservationStateMachine.CanCommit(StockReservationStatus.Committed));
    }

    [Fact]
    public void CanCommit_WhenReleased_ReturnsFalse_Forbidden()
    {
        Assert.False(StockReservationStateMachine.CanCommit(StockReservationStatus.Released));
    }

    [Fact]
    public void CanRelease_WhenReserved_ReturnsTrue()
    {
        Assert.True(StockReservationStateMachine.CanRelease(StockReservationStatus.Reserved));
    }

    [Fact]
    public void CanRelease_WhenReleased_ReturnsTrue_ForIdempotency()
    {
        Assert.True(StockReservationStateMachine.CanRelease(StockReservationStatus.Released));
    }

    [Fact]
    public void CanRelease_WhenCommitted_ReturnsFalse_Forbidden()
    {
        Assert.False(StockReservationStateMachine.CanRelease(StockReservationStatus.Committed));
    }
}
