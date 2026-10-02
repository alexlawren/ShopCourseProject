namespace Shop.CatalogService.Infrastructure.Storage;

public interface IProductImageStorage
{
    /// <summary>
    /// Saves the given image stream to storage with a secure server-generated filename.
    /// </summary>
    /// <param name="stream">The image content stream.</param>
    /// <param name="extension">The normalized file extension (e.g. ".jpg", ".png", ".webp").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The public relative URL path of the saved image (e.g. "/product-images/1234567890abcdef1234567890abcdef.png").</returns>
    Task<string> SaveAsync(Stream stream, string extension, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the image corresponding to the provided public relative path from storage.
    /// If the file does not exist, the operation completes successfully without error.
    /// </summary>
    /// <param name="publicPath">The public relative URL path of the image to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string? publicPath, CancellationToken cancellationToken = default);
}
