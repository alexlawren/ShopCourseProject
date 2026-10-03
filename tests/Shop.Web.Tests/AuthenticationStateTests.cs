using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Shop.Web.Auth;

namespace Shop.Web.Tests;

public class AuthenticationStateTests
{
    private class InMemoryTokenStorage : ITokenStorage
    {
        private string? _accessToken;
        private string? _refreshToken;

        public Task<string?> GetAccessTokenAsync() => Task.FromResult(_accessToken);
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult(_refreshToken);

        public Task SetTokensAsync(string accessToken, string refreshToken)
        {
            _accessToken = accessToken;
            _refreshToken = refreshToken;
            return Task.CompletedTask;
        }

        public Task ClearTokensAsync()
        {
            _accessToken = null;
            _refreshToken = null;
            return Task.CompletedTask;
        }
    }

    private static string CreateTestJwt(string userId, string email, string role)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            { "sub", userId },
            { "email", email },
            { "role", role }
        });
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{header}.{payloadBase64}.mock_signature";
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_NoToken_ReturnsAnonymous()
    {
        var storage = new InMemoryTokenStorage();
        var provider = new CustomAuthenticationStateProvider(storage);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.NotNull(state.User);
        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ValidToken_ReturnsAuthenticatedUser()
    {
        var storage = new InMemoryTokenStorage();
        var userId = Guid.NewGuid().ToString();
        var token = CreateTestJwt(userId, "user@shop.local", "Customer");
        await storage.SetTokensAsync(token, "refresh-123");

        var provider = new CustomAuthenticationStateProvider(storage);
        var state = await provider.GetAuthenticationStateAsync();

        Assert.NotNull(state.User);
        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("user@shop.local", state.User.Identity?.Name);
        Assert.True(state.User.IsInRole("Customer"));
        Assert.False(state.User.IsInRole("Admin"));
    }

    [Fact]
    public async Task NotifyUserAuthentication_UpdatesStateToAuthenticated()
    {
        var storage = new InMemoryTokenStorage();
        var provider = new CustomAuthenticationStateProvider(storage);

        var initialState = await provider.GetAuthenticationStateAsync();
        Assert.False(initialState.User.Identity?.IsAuthenticated);

        var token = CreateTestJwt("admin-id", "admin@shop.local", "Admin");
        provider.NotifyUserAuthentication(token);

        var updatedState = await provider.GetAuthenticationStateAsync();
        Assert.True(updatedState.User.Identity?.IsAuthenticated);
        Assert.True(updatedState.User.IsInRole("Admin"));
    }

    [Fact]
    public async Task NotifyUserLogout_UpdatesStateToAnonymous()
    {
        var storage = new InMemoryTokenStorage();
        var token = CreateTestJwt("user-id", "user@shop.local", "Customer");
        await storage.SetTokensAsync(token, "refresh");

        var provider = new CustomAuthenticationStateProvider(storage);
        provider.NotifyUserAuthentication(token);

        var stateBefore = await provider.GetAuthenticationStateAsync();
        Assert.True(stateBefore.User.Identity?.IsAuthenticated);

        provider.NotifyUserLogout();

        var stateAfter = await provider.GetAuthenticationStateAsync();
        Assert.False(stateAfter.User.Identity?.IsAuthenticated);
    }
}
