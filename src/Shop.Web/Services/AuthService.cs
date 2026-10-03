using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Shop.Web.Auth;
using Shop.Web.Models.Auth;

namespace Shop.Web.Services;

public record AuthResult(bool Succeeded, string? ErrorMessage = null);

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterModel model);
    Task<AuthResult> LoginAsync(LoginModel model);
    Task LogoutAsync();
    Task<bool> RefreshAsync();
    Task<UserDtoModel?> GetCurrentUserAsync();
    Task RestoreSessionAsync();
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly CustomAuthenticationStateProvider _authStateProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthService(
        HttpClient httpClient,
        ITokenStorage tokenStorage,
        AuthenticationStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
        _authStateProvider = (CustomAuthenticationStateProvider)authStateProvider;
    }

    public async Task<AuthResult> RegisterAsync(RegisterModel model)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/register", new
            {
                email = model.Email,
                password = model.Password
            });

            if (response.IsSuccessStatusCode)
            {
                var authData = await response.Content.ReadFromJsonAsync<AuthResponseModel>();
                if (authData != null)
                {
                    await _tokenStorage.SetTokensAsync(authData.AccessToken, authData.RefreshToken);
                    _authStateProvider.NotifyUserAuthentication(authData.AccessToken);
                    return new AuthResult(true);
                }
            }

            var errorMsg = await ParseErrorMessageAsync(response);
            return new AuthResult(false, errorMsg);
        }
        catch (Exception ex)
        {
            return new AuthResult(false, $"Ошибка сети при регистрации: {ex.Message}");
        }
    }

    public async Task<AuthResult> LoginAsync(LoginModel model)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new
            {
                email = model.Email,
                password = model.Password
            });

            if (response.IsSuccessStatusCode)
            {
                var authData = await response.Content.ReadFromJsonAsync<AuthResponseModel>();
                if (authData != null)
                {
                    await _tokenStorage.SetTokensAsync(authData.AccessToken, authData.RefreshToken);
                    _authStateProvider.NotifyUserAuthentication(authData.AccessToken);
                    return new AuthResult(true);
                }
            }

            var errorMsg = await ParseErrorMessageAsync(response);
            return new AuthResult(false, errorMsg);
        }
        catch (Exception ex)
        {
            return new AuthResult(false, $"Ошибка сети при входе: {ex.Message}");
        }
    }

    public async Task LogoutAsync()
    {
        var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            try
            {
                await _httpClient.PostAsJsonAsync("/api/auth/logout", new RefreshTokenRequestModel
                {
                    RefreshToken = refreshToken
                });
            }
            catch
            {
                // Ignore network errors on logout
            }
        }

        await _tokenStorage.ClearTokensAsync();
        _authStateProvider.NotifyUserLogout();
    }

    public async Task<bool> RefreshAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                await LogoutAsync();
                return false;
            }

            var response = await _httpClient.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestModel
            {
                RefreshToken = refreshToken
            });

            if (response.IsSuccessStatusCode)
            {
                var authData = await response.Content.ReadFromJsonAsync<AuthResponseModel>();
                if (authData != null)
                {
                    await _tokenStorage.SetTokensAsync(authData.AccessToken, authData.RefreshToken);
                    _authStateProvider.NotifyUserAuthentication(authData.AccessToken);
                    return true;
                }
            }

            await LogoutAsync();
            return false;
        }
        catch
        {
            await LogoutAsync();
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<UserDtoModel?> GetCurrentUserAsync()
    {
        try
        {
            var token = await _tokenStorage.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
                return null;

            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserDtoModel>();
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var refreshed = await RefreshAsync();
                if (refreshed)
                {
                    var newToken = await _tokenStorage.GetAccessTokenAsync();
                    using var retryRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);

                    var retryResponse = await _httpClient.SendAsync(retryRequest);
                    if (retryResponse.IsSuccessStatusCode)
                    {
                        return await retryResponse.Content.ReadFromJsonAsync<UserDtoModel>();
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task RestoreSessionAsync()
    {
        var token = await _tokenStorage.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            _authStateProvider.NotifyUserAuthentication(token);
        }
    }

    private static async Task<string> ParseErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
                return $"Запрос завершился с кодом {(int)response.StatusCode}";

            var problem = JsonSerializer.Deserialize<ProblemDetailsModel>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (problem != null)
            {
                var msg = problem.GetDisplayMessage();
                if (!string.IsNullOrWhiteSpace(msg))
                    return msg;
            }

            return content;
        }
        catch
        {
            return $"Ошибка HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";
        }
    }
}
