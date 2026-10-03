using Microsoft.AspNetCore.SignalR.Client;
using Shop.Web.Auth;
using Shop.Web.Models.Orders;

namespace Shop.Web.Services;

public interface IOrderRealtimeService
{
    event Action<OrderCreatedEvent>? OnOrderCreated;
    event Action<OrderStatusChangedEvent>? OnOrderStatusChanged;
    event Action<PaymentStatusChangedEvent>? OnPaymentStatusChanged;
    Task StartAsync();
    Task StopAsync();
    HubConnectionState State { get; }
}

public class OrderRealtimeService : IOrderRealtimeService, IAsyncDisposable
{
    private readonly HubConnection _hubConnection;
    private readonly ITokenStorage _tokenStorage;
    private bool _isStarted;

    public event Action<OrderCreatedEvent>? OnOrderCreated;
    public event Action<OrderStatusChangedEvent>? OnOrderStatusChanged;
    public event Action<PaymentStatusChangedEvent>? OnPaymentStatusChanged;

    public HubConnectionState State => _hubConnection.State;

    public OrderRealtimeService(IConfiguration configuration, ITokenStorage tokenStorage)
    {
        _tokenStorage = tokenStorage;
        var gatewayUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5210";
        var hubUrl = $"{gatewayUrl.TrimEnd('/')}/hubs/orders";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = async () =>
                {
                    var token = await _tokenStorage.GetAccessTokenAsync();
                    return token;
                };
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<OrderCreatedEvent>("OrderCreated", ev =>
        {
            OnOrderCreated?.Invoke(ev);
        });

        _hubConnection.On<OrderStatusChangedEvent>("OrderStatusChanged", ev =>
        {
            OnOrderStatusChanged?.Invoke(ev);
        });

        _hubConnection.On<PaymentStatusChangedEvent>("PaymentStatusChanged", ev =>
        {
            OnPaymentStatusChanged?.Invoke(ev);
        });
    }

    public async Task StartAsync()
    {
        var token = await _tokenStorage.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        if (_isStarted || _hubConnection.State != HubConnectionState.Disconnected)
            return;

        try
        {
            await _hubConnection.StartAsync();
            _isStarted = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OrderRealtimeService] Could not connect to OrderHub: {ex.Message}");
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection.State != HubConnectionState.Disconnected)
        {
            try
            {
                await _hubConnection.StopAsync();
            }
            catch
            {
                // Ignore
            }
        }

        _isStarted = false;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _hubConnection.DisposeAsync();
    }
}
