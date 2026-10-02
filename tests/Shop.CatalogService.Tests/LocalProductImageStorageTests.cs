using Microsoft.Extensions.Logging.Abstractions;
using Shop.CatalogService.Infrastructure.Storage;

namespace Shop.CatalogService.Tests;

public sealed class LocalProductImageStorageTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly LocalProductImageStorage _storage;

    public LocalProductImageStorageTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "shop_catalog_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);

        _storage = new LocalProductImageStorage(
            _testDirectory,
            "/product-images",
            NullLogger<LocalProductImageStorage>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    [Fact]
    public async Task SaveAsync_CreatesFileWithGeneratedNameAndReturnsPublicPath()
    {
        byte[] content = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        using var stream = new MemoryStream(content);

        var publicPath = await _storage.SaveAsync(stream, ".png");

        Assert.NotNull(publicPath);
        Assert.StartsWith("/product-images/", publicPath);
        Assert.EndsWith(".png", publicPath);

        var fileName = Path.GetFileName(publicPath);
        var physicalPath = Path.Combine(_testDirectory, fileName);

        Assert.True(File.Exists(physicalPath));
        var savedBytes = await File.ReadAllBytesAsync(physicalPath);
        Assert.Equal(content, savedBytes);
    }

    [Fact]
    public async Task SaveAsync_GeneratedFilenameDoesNotUseOriginalClientFilename()
    {
        byte[] content = [0x01, 0x02];
        using var stream = new MemoryStream(content);

        var path1 = await _storage.SaveAsync(stream, ".jpg");
        stream.Position = 0;
        var path2 = await _storage.SaveAsync(stream, ".jpg");

        Assert.NotEqual(path1, path2);
    }

    [Fact]
    public async Task DeleteAsync_DeletesExistingPhysicalFile()
    {
        byte[] content = [0x01, 0x02, 0x03];
        using var stream = new MemoryStream(content);

        var publicPath = await _storage.SaveAsync(stream, ".webp");
        var physicalPath = Path.Combine(_testDirectory, Path.GetFileName(publicPath));
        Assert.True(File.Exists(physicalPath));

        await _storage.DeleteAsync(publicPath);

        Assert.False(File.Exists(physicalPath));
    }

    [Fact]
    public async Task DeleteAsync_NonExistentFile_DoesNotThrow()
    {
        var nonExistentPath = "/product-images/non_existent_file.png";
        var exception = await Record.ExceptionAsync(() => _storage.DeleteAsync(nonExistentPath));

        Assert.Null(exception);
    }

    [Fact]
    public async Task DeleteAsync_PathTraversalAttempt_DoesNotDeleteTargetFile()
    {
        // Create a sensitive file outside storage directory
        var parentDir = Path.GetDirectoryName(_testDirectory)!;
        var outsideFile = Path.Combine(parentDir, "outside_file_" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(outsideFile, "secret");

        try
        {
            var relativeTraversal = $"/product-images/../{Path.GetFileName(outsideFile)}";
            await _storage.DeleteAsync(relativeTraversal);

            Assert.True(File.Exists(outsideFile));
        }
        finally
        {
            if (File.Exists(outsideFile))
            {
                File.Delete(outsideFile);
            }
        }
    }
}
