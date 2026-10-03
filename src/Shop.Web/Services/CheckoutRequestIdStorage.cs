using Microsoft.JSInterop;

namespace Shop.Web.Services;

public interface ICheckoutRequestIdStorage
{
    Task<Guid?> GetPendingRequestIdAsync();
    Task SetPendingRequestIdAsync(Guid requestId);
    Task ClearPendingRequestIdAsync();
}

public class SessionStorageCheckoutRequestIdStorage : ICheckoutRequestIdStorage
{
    private const string StorageKey = "pending_checkout_request_id";
    private readonly IJSRuntime _jsRuntime;

    public SessionStorageCheckoutRequestIdStorage(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<Guid?> GetPendingRequestIdAsync()
    {
        try
        {
            var str = await _jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
            if (!string.IsNullOrWhiteSpace(str) && Guid.TryParse(str, out var id) && id != Guid.Empty)
            {
                return id;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetPendingRequestIdAsync(Guid requestId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", StorageKey, requestId.ToString());
        }
        catch
        {
            // Ignore JS exceptions
        }
    }

    public async Task ClearPendingRequestIdAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
        }
        catch
        {
            // Ignore JS exceptions
        }
    }
}
