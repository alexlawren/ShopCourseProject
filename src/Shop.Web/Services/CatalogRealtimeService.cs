using Microsoft.AspNetCore.SignalR.Client;
using Shop.Web.Models.Catalog;

namespace Shop.Web.Services;

public interface ICatalogRealtimeService
{
    event Action<ProductChangedEvent>? OnProductChanged;
    event Action<StockChangedEvent>? OnStockChanged;
    Task StartAsync();
    Task StopAsync();
    HubConnectionState State { get; }
}

public class CatalogRealtimeService : ICatalogRealtimeService, IAsyncDisposable
{
    private readonly HubConnection _hubConnection;
    private bool _isStarted;

    public event Action<ProductChangedEvent>? OnProductChanged;
    public event Action<StockChangedEvent>? OnStockChanged;

    public HubConnectionState State => _hubConnection.State;

    public CatalogRealtimeService(IConfiguration configuration)
    {
        var gatewayUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5210";
        var hubUrl = $"{gatewayUrl.TrimEnd('/')}/hubs/catalog";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<ProductChangedEvent>("ProductChanged", ev =>
        {
            OnProductChanged?.Invoke(ev);
        });

        _hubConnection.On<StockChangedEvent>("StockChanged", ev =>
        {
            OnStockChanged?.Invoke(ev);
        });
    }

    public async Task StartAsync()
    {
        if (_isStarted || _hubConnection.State != HubConnectionState.Disconnected)
            return;

        try
        {
            await _hubConnection.StartAsync();
            _isStarted = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CatalogRealtimeService] Could not connect to CatalogHub: {ex.Message}");
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
