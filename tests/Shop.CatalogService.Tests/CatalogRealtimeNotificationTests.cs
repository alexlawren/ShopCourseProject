using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Shop.CatalogService.Application.Catalog.Notifications;
using Shop.CatalogService.Hubs;
using Shop.CatalogService.Infrastructure.Notifications;

namespace Shop.CatalogService.Tests;

public class CatalogRealtimeNotificationTests
{
    [Fact]
    public void CatalogHubEvents_Constants_MatchExpected()
    {
        Assert.Equal("ProductChanged", CatalogHubEvents.ProductChanged);
        Assert.Equal("StockChanged", CatalogHubEvents.StockChanged);
    }

    [Fact]
    public void CatalogEvents_PayloadContracts_DoNotExposeInternalOrSensitiveFields()
    {
        var productProperties = typeof(ProductChangedEvent).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("ReservationId", productProperties);
        Assert.DoesNotContain("RequestId", productProperties);
        Assert.DoesNotContain("Price", productProperties);

        var stockProperties = typeof(StockChangedEvent).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("ReservationId", stockProperties);
        Assert.DoesNotContain("RequestId", stockProperties);
        Assert.DoesNotContain("Price", stockProperties);
    }

    [Fact]
    public async Task SignalRCatalogNotificationService_WhenHubThrows_SuppressesException()
    {
        var throwingHubContext = new ThrowingCatalogHubContext();
        var service = new SignalRCatalogNotificationService(throwingHubContext, NullLogger<SignalRCatalogNotificationService>.Instance);

        var productId = Guid.NewGuid();

        // Must not throw any exception
        await service.NotifyProductChangedAsync(
            new ProductChangedEvent(productId, true, DateTime.UtcNow));

        await service.NotifyStockChangedAsync(
            new StockChangedEvent(productId, 42, DateTime.UtcNow));
    }

    private sealed class ThrowingCatalogHubContext : IHubContext<CatalogHub>
    {
        public IHubClients Clients => new ThrowingHubClients();
        public IGroupManager Groups => throw new NotImplementedException();
    }

    private sealed class ThrowingHubClients : IHubClients
    {
        public IClientProxy All => new ThrowingClientProxy();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy Client(string connectionId) => throw new NotImplementedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotImplementedException();
        public IClientProxy Group(string groupName) => throw new NotImplementedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotImplementedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy User(string userId) => throw new NotImplementedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
    }

    private sealed class ThrowingClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated SignalR broadcast socket failure.");
        }
    }
}
