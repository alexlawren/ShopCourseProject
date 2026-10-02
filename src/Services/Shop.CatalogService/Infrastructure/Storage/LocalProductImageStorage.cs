using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shop.CatalogService.Application.Catalog.Images;

namespace Shop.CatalogService.Infrastructure.Storage;

public sealed class LocalProductImageStorage : IProductImageStorage
{
    private readonly string _storageRootDirectory;
    private readonly string _requestPath;
    private readonly ILogger<LocalProductImageStorage> _logger;

    public LocalProductImageStorage(
        IOptions<ProductImageOptions> options,
        IWebHostEnvironment environment,
        ILogger<LocalProductImageStorage> logger)
        : this(
            ResolveStoragePath(environment.ContentRootPath, options.Value.StoragePath),
            options.Value.RequestPath,
            logger)
    {
    }

    public LocalProductImageStorage(
        string storageRootDirectory,
        string requestPath,
        ILogger<LocalProductImageStorage> logger)
    {
        _storageRootDirectory = Path.GetFullPath(storageRootDirectory);
        _requestPath = "/" + (requestPath ?? "product-images").Trim('/');
        _logger = logger;

        Directory.CreateDirectory(_storageRootDirectory);
    }

    private static string ResolveStoragePath(string contentRootPath, string configuredPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRootPath, configuredPath);
    }

    public async Task<string> SaveAsync(Stream stream, string extension, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var safeExt = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{safeExt}";
        var destinationPath = Path.GetFullPath(Path.Combine(_storageRootDirectory, fileName));

        // Path traversal defense
        if (!destinationPath.StartsWith(_storageRootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid path traversal detected when saving product image.");
        }

        await using (var fileStream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        return $"{_requestPath}/{fileName}";
    }

    public Task DeleteAsync(string? publicPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicPath))
        {
            return Task.CompletedTask;
        }

        if (!publicPath.StartsWith(_requestPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Ignoring delete for image path '{PublicPath}' outside request path '{RequestPath}'.", publicPath, _requestPath);
            return Task.CompletedTask;
        }

        var fileName = Path.GetFileName(publicPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Task.CompletedTask;
        }

        var fullPath = Path.GetFullPath(Path.Combine(_storageRootDirectory, fileName));

        // Path traversal defense
        if (!fullPath.StartsWith(_storageRootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Potential path traversal attempt detected while deleting image: '{PublicPath}'.", publicPath);
            return Task.CompletedTask;
        }

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete physical image file '{FilePath}'.", fullPath);
        }

        return Task.CompletedTask;
    }
}
