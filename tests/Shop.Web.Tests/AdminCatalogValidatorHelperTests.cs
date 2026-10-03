using Shop.Web.Services;

namespace Shop.Web.Tests;

public class AdminCatalogValidatorHelperTests
{
    [Theory]
    [InlineData("laptops")]
    [InlineData("gaming-laptops")]
    [InlineData("smartphones-2026")]
    [InlineData("a")]
    [InlineData("123")]
    [InlineData("lenovo-thinkpad-x1-carbon")]
    public void IsValidSlug_ValidSlug_ReturnsTrue(string slug)
    {
        var result = AdminCatalogValidatorHelper.IsValidSlug(slug);
        Assert.True(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("Laptops")] // uppercase not allowed by backend regex
    [InlineData("gaming_laptops")] // underscore not allowed
    [InlineData("-leading-hyphen")]
    [InlineData("trailing-hyphen-")]
    [InlineData("double--hyphen")]
    [InlineData("кириллица")]
    public void IsValidSlug_InvalidSlug_ReturnsFalse(string? slug)
    {
        var result = AdminCatalogValidatorHelper.IsValidSlug(slug);
        Assert.False(result);
    }

    [Fact]
    public void IsValidSlug_ExceedingMaxLength_ReturnsFalse()
    {
        var slug = new string('a', 141);
        var result = AdminCatalogValidatorHelper.IsValidSlug(slug);
        Assert.False(result);
    }

    [Theory]
    [InlineData("image.jpg", "image/jpeg", 1024)]
    [InlineData("photo.jpeg", "image/jpeg", 5 * 1024 * 1024)]
    [InlineData("picture.png", "image/png", 500)]
    [InlineData("graphic.webp", "image/webp", 2048)]
    public void ValidateImage_ValidImage_ReturnsTrue(string fileName, string contentType, long size)
    {
        var valid = AdminCatalogValidatorHelper.ValidateImage(fileName, contentType, size, out var error);
        Assert.True(valid);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("image.gif", "image/gif", 1024)]
    [InlineData("document.pdf", "application/pdf", 1024)]
    [InlineData("script.exe", "application/octet-stream", 1024)]
    [InlineData("vector.svg", "image/svg+xml", 1024)]
    public void ValidateImage_InvalidExtension_ReturnsFalse(string fileName, string contentType, long size)
    {
        var valid = AdminCatalogValidatorHelper.ValidateImage(fileName, contentType, size, out var error);
        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains("Недопустимое расширение", error);
    }

    [Theory]
    [InlineData("photo.png", "application/json", 1024)]
    [InlineData("photo.jpg", "text/plain", 1024)]
    public void ValidateImage_InvalidContentType_ReturnsFalse(string fileName, string contentType, long size)
    {
        var valid = AdminCatalogValidatorHelper.ValidateImage(fileName, contentType, size, out var error);
        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains("Недопустимый тип содержимого", error);
    }

    [Fact]
    public void ValidateImage_Exceeds5MiB_ReturnsFalse()
    {
        var valid = AdminCatalogValidatorHelper.ValidateImage("big.jpg", "image/jpeg", 5 * 1024 * 1024 + 1, out var error);
        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains("5 МБ", error);
    }

    [Fact]
    public void ValidateImage_EmptyFile_ReturnsFalse()
    {
        var valid = AdminCatalogValidatorHelper.ValidateImage("empty.jpg", "image/jpeg", 0, out var error);
        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains("пуст", error);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0.01, true)]
    [InlineData(999.99, true)]
    [InlineData(-0.01, false)]
    [InlineData(-100, false)]
    public void IsValidPrice_ChecksNonNegative(decimal price, bool expected)
    {
        var result = AdminCatalogValidatorHelper.IsValidPrice(price);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(-1, false)]
    [InlineData(-50, false)]
    public void IsValidStock_ChecksNonNegative(int stock, bool expected)
    {
        var result = AdminCatalogValidatorHelper.IsValidStock(stock);
        Assert.Equal(expected, result);
    }
}
