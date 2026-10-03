using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Shop.OrderService.Application.Orders.Notifications;
using Shop.OrderService.Controllers.Common;
using Shop.OrderService.Hubs;
using Shop.OrderService.Infrastructure.Notifications;

namespace Shop.OrderService.Tests;

public class OrderRealtimeNotificationTests
{
    [Fact]
    public void OrderHubGroups_UserGroup_FormatsCorrectly()
    {
        var userId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var group = OrderHubGroups.User(userId);

        Assert.Equal("user:12345678-1234-1234-1234-123456789abc", group);
    }

    [Fact]
    public void OrderHubGroups_AdminsGroup_MatchesConstant()
    {
        Assert.Equal("admins", OrderHubGroups.Admins);
    }

    [Fact]
    public void OrderHubEvents_Constants_MatchExpected()
    {
        Assert.Equal("OrderCreated", OrderHubEvents.OrderCreated);
        Assert.Equal("OrderStatusChanged", OrderHubEvents.OrderStatusChanged);
        Assert.Equal("PaymentStatusChanged", OrderHubEvents.PaymentStatusChanged);
    }

    [Fact]
    public void OrderEvents_PayloadContracts_DoNotExposeInternalOrSensitiveFields()
    {
        var createdProperties = typeof(OrderCreatedEvent).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("ReservationId", createdProperties);
        Assert.DoesNotContain("CheckoutRequestId", createdProperties);
        Assert.DoesNotContain("CancellationState", createdProperties);
        Assert.DoesNotContain("Token", createdProperties);

        var statusProperties = typeof(OrderStatusChangedEvent).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("ReservationId", statusProperties);
        Assert.DoesNotContain("CancellationState", statusProperties);

        var paymentProperties = typeof(PaymentStatusChangedEvent).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("ReservationId", paymentProperties);
        Assert.DoesNotContain("CancellationState", paymentProperties);
    }

    [Fact]
    public void ClaimsPrincipalExtensions_TryGetUserId_ValidSub_ReturnsTrueAndGuid()
    {
        var expectedId = Guid.NewGuid();
        var claims = new[] { new Claim("sub", expectedId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var result = principal.TryGetUserId(out var userId);

        Assert.True(result);
        Assert.Equal(expectedId, userId);
    }

    [Fact]
    public void ClaimsPrincipalExtensions_TryGetUserId_InvalidSub_ReturnsFalse()
    {
        var claims = new[] { new Claim("sub", "not-a-guid") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var result = principal.TryGetUserId(out var userId);

        Assert.False(result);
        Assert.Equal(Guid.Empty, userId);
    }

    [Fact]
    public void ClaimsPrincipalExtensions_TryGetUserId_MissingClaim_ReturnsFalse()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = principal.TryGetUserId(out var userId);

        Assert.False(result);
        Assert.Equal(Guid.Empty, userId);
    }

    [Fact]
    public async Task SignalROrderNotificationService_WhenHubThrows_SuppressesException()
    {
        var throwingHubContext = new ThrowingOrderHubContext();
        var service = new SignalROrderNotificationService(throwingHubContext, NullLogger<SignalROrderNotificationService>.Instance);

        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Must not throw any exception
        await service.NotifyOrderCreatedAsync(
            new OrderCreatedEvent(orderId, "Created", "Pending", 100m, DateTime.UtcNow),
            userId);

        await service.NotifyOrderStatusChangedAsync(
            new OrderStatusChangedEvent(orderId, "Processing", DateTime.UtcNow),
            userId);

        await service.NotifyPaymentStatusChangedAsync(
            new PaymentStatusChangedEvent(orderId, "Paid", DateTime.UtcNow),
            userId);
    }

    private sealed class ThrowingOrderHubContext : IHubContext<OrderHub>
    {
        public IHubClients Clients => new ThrowingHubClients();
        public IGroupManager Groups => throw new NotImplementedException();
    }

    private sealed class ThrowingHubClients : IHubClients
    {
        public IClientProxy All => throw new NotImplementedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy Client(string connectionId) => throw new NotImplementedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotImplementedException();
        public IClientProxy Group(string groupName) => throw new NotImplementedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new ThrowingClientProxy();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy User(string userId) => throw new NotImplementedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
    }

    private sealed class ThrowingClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated SignalR network/socket failure.");
        }
    }
}
