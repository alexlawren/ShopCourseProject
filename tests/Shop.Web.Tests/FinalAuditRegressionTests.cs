using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Shop.Web.Auth;
using Shop.Web.Models.Auth;
using Shop.Web.Services;

namespace Shop.Web.Tests;

public class FinalAuditRegressionTests
{
    [Theory]
    [InlineData("A category with slug 'smartphones' already exists.", "Категория с таким Slug уже существует.")]
    [InlineData("Invalid email or password.", "Неверная электронная почта или пароль.")]
    [InlineData("Email already registered.", "Эта электронная почта уже зарегистрирована.")]
    public void KnownBackendTextIsLocalized(string detail, string expected)
    {
        Assert.Equal(expected, new ProblemDetailsModel { Detail = detail }.GetDisplayMessage());
    }

    [Fact]
    public void InternalErrorIsNotExposed()
    {
        var problem = new ProblemDetailsModel { Status = 500, Detail = "NpgsqlException: password=secret", Title = "Internal Server Error" };
        Assert.Equal("Сервис временно недоступен. Попробуйте позже.", problem.GetDisplayMessage());
    }

    [Fact]
    public void PasswordValidationIsLocalized()
    {
        var problem = new ProblemDetailsModel
        {
            Status = 400,
            Errors = new() { ["PasswordTooShort"] = ["Passwords must be at least 8 characters."] }
        };
        Assert.Equal("Пароль должен содержать минимум 8 символов.", problem.GetDisplayMessage());
    }

    [Fact]
    public void RegistrationRejectsPasswordThatBackendCannotAccept()
    {
        var model = new RegisterModel { Email = "user@example.com", Password = "abcdef", ConfirmPassword = "abcdef" };
        var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.False(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, new(model), errors, true));
    }

    [Fact]
    public async Task LogoutWithExpiredAccessTokenDoesNotRefresh()
    {
        var auth = new TrackingAuthService();
        using var services = new ServiceCollection().AddSingleton<IAuthService>(auth).BuildServiceProvider();
        using var handler = new AuthHeaderHandler(new ExpiredTokenStorage(), services) { InnerHandler = new SuccessHandler() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://gateway.test") };
        using var response = await client.PostAsync("/api/auth/logout", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, auth.RefreshCalls);
    }

    private sealed class SuccessHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }

    private sealed class ExpiredTokenStorage : ITokenStorage
    {
        public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>("e30." + Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"exp\":1}")).TrimEnd('=') + ".sig");
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult<string?>("refresh");
        public Task SetTokensAsync(string accessToken, string refreshToken) => Task.CompletedTask;
        public Task ClearTokensAsync() => Task.CompletedTask;
    }

    private sealed class TrackingAuthService : IAuthService
    {
        public int RefreshCalls { get; private set; }
        public Task<bool> RefreshAsync() { RefreshCalls++; return Task.FromResult(false); }
        public Task LogoutAsync() => Task.CompletedTask;
        public Task RestoreSessionAsync() => Task.CompletedTask;
        public Task<UserDtoModel?> GetCurrentUserAsync() => Task.FromResult<UserDtoModel?>(null);
        public Task<AuthResult> LoginAsync(LoginModel model) => Task.FromResult(new AuthResult(false));
        public Task<AuthResult> RegisterAsync(RegisterModel model) => Task.FromResult(new AuthResult(false));
    }
}
