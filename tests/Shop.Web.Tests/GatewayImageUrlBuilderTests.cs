using Shop.Web.Services;

namespace Shop.Web.Tests;

public class GatewayImageUrlBuilderTests
{
    private const string GatewayBase = "http://localhost:5210";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildImageUrl_NullOrEmptyPath_ReturnsEmptyString(string? path)
    {
        var result = GatewayImageUrlBuilder.BuildImageUrl(GatewayBase, path);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildImageUrl_RelativePathWithLeadingSlash_PrependsGatewayUrl()
    {
        var path = "/product-images/12345.png";
        var result = GatewayImageUrlBuilder.BuildImageUrl(GatewayBase, path);

        Assert.Equal("http://localhost:5210/product-images/12345.png", result);
    }

    [Fact]
    public void BuildImageUrl_RelativePathWithoutLeadingSlash_PrependsGatewayUrlWithSlash()
    {
        var path = "product-images/67890.jpg";
        var result = GatewayImageUrlBuilder.BuildImageUrl(GatewayBase, path);

        Assert.Equal("http://localhost:5210/product-images/67890.jpg", result);
    }

    [Fact]
    public void BuildImageUrl_NeverContainsDirectCatalogServicePort()
    {
        var path = "/product-images/item.png";
        var result = GatewayImageUrlBuilder.BuildImageUrl(GatewayBase, path);

        Assert.DoesNotContain("5058", result);
        Assert.StartsWith(GatewayBase, result);
    }

    [Fact]
    public void BuildImageUrl_GatewayUrlWithTrailingSlash_DoesNotDuplicateSlash()
    {
        var gatewayWithSlash = "http://localhost:5210/";
        var path = "/product-images/item.png";
        var result = GatewayImageUrlBuilder.BuildImageUrl(gatewayWithSlash, path);

        Assert.Equal("http://localhost:5210/product-images/item.png", result);
    }
}
