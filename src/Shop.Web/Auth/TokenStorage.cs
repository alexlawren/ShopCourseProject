using Microsoft.JSInterop;

namespace Shop.Web.Auth;

public interface ITokenStorage
{
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    Task SetTokensAsync(string accessToken, string refreshToken);
    Task ClearTokensAsync();
}

public class SessionStorageTokenStorage : ITokenStorage
{
    private const string AccessTokenKey = "shop_access_token";
    private const string RefreshTokenKey = "shop_refresh_token";
    private readonly IJSRuntime _jsRuntime;

    public SessionStorageTokenStorage(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", AccessTokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", RefreshTokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetTokensAsync(string accessToken, string refreshToken)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", AccessTokenKey, accessToken);
            await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", RefreshTokenKey, refreshToken);
        }
        catch
        {
            // Ignore JS interop exceptions during pre-rendering or testing
        }
    }

    public async Task ClearTokensAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", AccessTokenKey);
            await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", RefreshTokenKey);
        }
        catch
        {
            // Ignore
        }
    }
}
