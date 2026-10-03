using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Shop.Web.Auth;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStorage _tokenStorage;
    private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private AuthenticationState? _cachedState;

    public CustomAuthenticationStateProvider(ITokenStorage tokenStorage)
    {
        _tokenStorage = tokenStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_cachedState != null)
        {
            return _cachedState;
        }

        var token = await _tokenStorage.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AnonymousState;
        }

        var claims = JwtClaimsParser.ParseClaimsFromJwt(token).ToList();
        if (claims.Count == 0)
        {
            return AnonymousState;
        }

        var identity = new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role);
        _cachedState = new AuthenticationState(new ClaimsPrincipal(identity));
        return _cachedState;
    }

    public void NotifyUserAuthentication(string token)
    {
        var claims = JwtClaimsParser.ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role);
        var user = new ClaimsPrincipal(identity);
        _cachedState = new AuthenticationState(user);
        NotifyAuthenticationStateChanged(Task.FromResult(_cachedState));
    }

    public void NotifyUserLogout()
    {
        _cachedState = AnonymousState;
        NotifyAuthenticationStateChanged(Task.FromResult(AnonymousState));
    }
}
