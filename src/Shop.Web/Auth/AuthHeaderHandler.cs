using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Shop.Web.Services;

namespace Shop.Web.Auth;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly ITokenStorage _tokenStorage;
    private readonly IServiceProvider _serviceProvider;

    public AuthHeaderHandler(ITokenStorage tokenStorage, IServiceProvider serviceProvider)
    {
        _tokenStorage = tokenStorage;
        _serviceProvider = serviceProvider;
        InnerHandler = new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var isAuthEndpoint = path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
                             path.StartsWith("/api/auth/register", StringComparison.OrdinalIgnoreCase) ||
                             path.StartsWith("/api/auth/refresh", StringComparison.OrdinalIgnoreCase);

        if (!isAuthEndpoint)
        {
            var token = await _tokenStorage.GetAccessTokenAsync();
            var refreshToken = await _tokenStorage.GetRefreshTokenAsync();

            // Safe mutation refresh: proactively refresh if token is expired or expiring soon (< 30s)
            // BEFORE sending the request, so mutations are sent with a valid token and never replayed blindly.
            if (!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(refreshToken) &&
                JwtClaimsParser.IsExpiredOrExpiringSoon(token))
            {
                var authService = _serviceProvider.GetRequiredService<IAuthService>();
                var refreshed = await authService.RefreshAsync();
                if (refreshed)
                {
                    token = await _tokenStorage.GetAccessTokenAsync();
                }
            }

            if (!string.IsNullOrWhiteSpace(token) && request.Headers.Authorization == null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        var response = await base.SendAsync(request, cancellationToken);

        // If unauthorized on a safe GET request and not an auth endpoint, attempt token refresh and retry once
        if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthEndpoint && request.Method == HttpMethod.Get)
        {
            var authService = _serviceProvider.GetRequiredService<IAuthService>();
            var refreshed = await authService.RefreshAsync();
            if (refreshed)
            {
                var newToken = await _tokenStorage.GetAccessTokenAsync();
                if (!string.IsNullOrWhiteSpace(newToken))
                {
                    using var retryRequest = new HttpRequestMessage(request.Method, request.RequestUri);
                    foreach (var header in request.Headers)
                    {
                        if (!header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                        {
                            retryRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
                    return await base.SendAsync(retryRequest, cancellationToken);
                }
            }
        }

        return response;
    }
}
