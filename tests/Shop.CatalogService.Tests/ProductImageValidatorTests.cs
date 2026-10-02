using Shop.CatalogService.Application.Catalog.Images;

namespace Shop.CatalogService.Tests;

public class ProductImageValidatorTests
{
    private readonly ProductImageValidator _validator = new();
    private const long MaxSizeBytes = 5 * 1024 * 1024; // 5 MiB

    private static readonly byte[] ValidPngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] ValidJpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
    private static readonly byte[] ValidWebpBytes =
    [
        0x52, 0x49, 0x46, 0x46, // RIFF
        0x24, 0x00, 0x00, 0x00, // size
        0x57, 0x45, 0x42, 0x50, // WEBP
        0x56, 0x50, 0x38, 0x20  // VP8
    ];

    [Fact]
    public void Validate_ValidPng_ReturnsSuccess()
    {
        using var stream = new MemoryStream(ValidPngBytes);
        var result = _validator.ValidateStream(stream, "photo.png", "image/png", ValidPngBytes.Length, MaxSizeBytes);

        Assert.True(result.IsValid);
        Assert.Equal(".png", result.NormalizedExtension);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("picture.jpg")]
    [InlineData("picture.jpeg")]
    public void Validate_ValidJpeg_ReturnsSuccessWithNormalizedJpgExtension(string fileName)
    {
        using var stream = new MemoryStream(ValidJpegBytes);
        var result = _validator.ValidateStream(stream, fileName, "image/jpeg", ValidJpegBytes.Length, MaxSizeBytes);

        Assert.True(result.IsValid);
        Assert.Equal(".jpg", result.NormalizedExtension);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_ValidWebp_ReturnsSuccess()
    {
        using var stream = new MemoryStream(ValidWebpBytes);
        var result = _validator.ValidateStream(stream, "banner.webp", "image/webp", ValidWebpBytes.Length, MaxSizeBytes);

        Assert.True(result.IsValid);
        Assert.Equal(".webp", result.NormalizedExtension);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_EmptyFile_ReturnsFailure()
    {
        using var stream = new MemoryStream();
        var result = _validator.ValidateStream(stream, "test.png", "image/png", 0, MaxSizeBytes);

        Assert.False(result.IsValid);
        Assert.Contains("empty", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("test.svg", "image/svg+xml")]
    [InlineData("test.gif", "image/gif")]
    [InlineData("test.bmp", "image/bmp")]
    [InlineData("test.txt", "text/plain")]
    [InlineData("test.exe", "application/octet-stream")]
    public void Validate_UnsupportedExtension_ReturnsFailure(string fileName, string contentType)
    {
        byte[] dummy = [0x01, 0x02, 0x03, 0x04];
        using var stream = new MemoryStream(dummy);
        var result = _validator.ValidateStream(stream, fileName, contentType, dummy.Length, MaxSizeBytes);

        Assert.False(result.IsValid);
        Assert.Contains("Unsupported", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExtensionContentTypeMismatch_ReturnsFailure()
    {
        using var stream = new MemoryStream(ValidPngBytes);
        // .png extension with image/jpeg Content-Type
        var result = _validator.ValidateStream(stream, "photo.png", "image/jpeg", ValidPngBytes.Length, MaxSizeBytes);

        Assert.False(result.IsValid);
        Assert.Contains("not valid", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_FakePngInvalidMagicBytes_ReturnsFailure()
    {
        byte[] fakePng = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09];
        using var stream = new MemoryStream(fakePng);
        var result = _validator.ValidateStream(stream, "fake.png", "image/png", fakePng.Length, MaxSizeBytes);

        Assert.False(result.IsValid);
        Assert.Contains("signature", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_FakeJpegInvalidMagicBytes_ReturnsFailure()
    {
        byte[] fakeJpeg = [0x89, 0x50, 0x4E, 0x47]; // PNG bytes in JPG file
        using var stream = new MemoryStream(fakeJpeg);
        var result = _validator.ValidateStream(stream, "fake.jpg", "image/jpeg", fakeJpeg.Length, MaxSizeBytes);

        Assert.False(result.IsValid);
        Assert.Contains("signature", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_FakeWebpInvalidMagicBytes_ReturnsFailure()
    {
        byte[] fakeWebp = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x41, 0x56, 0x49, 0x20]; // RIFF AVI instead of WEBP
        using var stream = new MemoryStream(fakeWebp);
        var result = _validator.ValidateStream(stream, "fake.webp", "image/webp", fakeWebp.Length, MaxSizeBytes);

        Assert.False(result.IsValid);
        Assert.Contains("signature", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_FileExceedsMaxSize_ReturnsFailure()
    {
        using var stream = new MemoryStream(ValidPngBytes);
        var result = _validator.ValidateStream(stream, "large.png", "image/png", MaxSizeBytes + 1, MaxSizeBytes);

        Assert.False(result.IsValid);
        Assert.Contains("exceeds the maximum allowed limit", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
