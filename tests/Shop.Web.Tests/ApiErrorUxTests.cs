using System.Net;
using Microsoft.Extensions.Configuration;
using Shop.Web.Models.Catalog;
using Shop.Web.Services;

namespace Shop.Web.Tests;

public class ApiErrorUxTests
{
    [Fact]
    public async Task CatalogUnavailableMustNotLookLikeEmptyCatalog()
    {
        using var client = CreateClient(HttpStatusCode.ServiceUnavailable);
        var api = new CatalogApiClient(client, new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<HttpRequestException>(() => api.GetProductsAsync(new CatalogQueryModel()));
    }

    [Fact]
    public async Task ProductUnavailableMustNotLookLikeMissingProduct()
    {
        using var client = CreateClient(HttpStatusCode.ServiceUnavailable);
        var api = new CatalogApiClient(client, new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<HttpRequestException>(() => api.GetProductByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MissingProductStillReturnsNull()
    {
        using var client = CreateClient(HttpStatusCode.NotFound);
        var api = new CatalogApiClient(client, new ConfigurationBuilder().Build());
        Assert.Null(await api.GetProductByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RawServerBodyIsNeverShownForCheckout()
    {
        using var client = CreateClient(HttpStatusCode.InternalServerError);
        var result = await new OrderApiClient(client).CheckoutAsync(Guid.NewGuid());
        Assert.False(result.Succeeded);
        Assert.Equal("Сервис временно недоступен. Попробуйте позже.", result.ErrorMessage);
    }

    private static HttpClient CreateClient(HttpStatusCode status) => new(new ErrorHandler(status)) { BaseAddress = new Uri("http://gateway.test") };

    private sealed class ErrorHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("\"internal password=secret\"") });
    }
}
