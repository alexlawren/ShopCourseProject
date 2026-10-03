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

    [Fact]
    public void CanCancelCommitted_WhenCommitted_ReturnsTrue()
    {
        Assert.True(StockReservationStateMachine.CanCancelCommitted(StockReservationStatus.Committed));
    }

    [Fact]
    public void CanCancelCommitted_WhenCancelled_ReturnsTrue_ForIdempotency()
    {
        Assert.True(StockReservationStateMachine.CanCancelCommitted(StockReservationStatus.Cancelled));
    }

    [Fact]
    public void CanCancelCommitted_WhenReserved_ReturnsFalse()
    {
        Assert.False(StockReservationStateMachine.CanCancelCommitted(StockReservationStatus.Reserved));
    }

    [Fact]
    public void CanCancelCommitted_WhenReleased_ReturnsFalse()
    {
        Assert.False(StockReservationStateMachine.CanCancelCommitted(StockReservationStatus.Released));
    }

    [Fact]
    public void CanCommit_WhenCancelled_ReturnsFalse()
    {
        Assert.False(StockReservationStateMachine.CanCommit(StockReservationStatus.Cancelled));
    }

    [Fact]
    public void CanRelease_WhenCancelled_ReturnsFalse()
    {
        Assert.False(StockReservationStateMachine.CanRelease(StockReservationStatus.Cancelled));
    }
}
