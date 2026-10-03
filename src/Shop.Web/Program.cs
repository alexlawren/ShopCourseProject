using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Shop.Web;
using Shop.Web.Auth;
using Shop.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var gatewayBaseUrl = builder.Configuration["Gateway:BaseUrl"];
if (string.IsNullOrWhiteSpace(gatewayBaseUrl))
{
    gatewayBaseUrl = builder.HostEnvironment.BaseAddress;
    builder.Configuration["Gateway:BaseUrl"] = gatewayBaseUrl;
}

builder.Services.AddScoped<ITokenStorage, SessionStorageTokenStorage>();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();

builder.Services.AddTransient<AuthHeaderHandler>();

builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthHeaderHandler>();
    return new HttpClient(handler)
    {
        BaseAddress = new Uri(gatewayBaseUrl)
    };
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICatalogApiClient, CatalogApiClient>();
builder.Services.AddScoped<ICatalogRealtimeService, CatalogRealtimeService>();
builder.Services.AddScoped<ICartApiClient, CartApiClient>();
builder.Services.AddScoped<IOrderApiClient, OrderApiClient>();
builder.Services.AddScoped<IOrderRealtimeService, OrderRealtimeService>();
builder.Services.AddScoped<ICheckoutRequestIdStorage, SessionStorageCheckoutRequestIdStorage>();
builder.Services.AddScoped<IAdminCatalogApiClient, AdminCatalogApiClient>();
builder.Services.AddScoped<IAdminOrderApiClient, AdminOrderApiClient>();

var host = builder.Build();

// Restore session state from sessionStorage on application startup
var authService = host.Services.GetRequiredService<IAuthService>();
await authService.RestoreSessionAsync();

await host.RunAsync();

