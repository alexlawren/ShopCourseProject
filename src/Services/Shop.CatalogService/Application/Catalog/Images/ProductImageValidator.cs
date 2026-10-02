using Microsoft.AspNetCore.Http;

namespace Shop.CatalogService.Application.Catalog.Images;

public sealed class ImageValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }
    public string? NormalizedExtension { get; }

    private ImageValidationResult(bool isValid, string? errorMessage, string? normalizedExtension)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
        NormalizedExtension = normalizedExtension;
    }

    public static ImageValidationResult Success(string normalizedExtension) =>
        new(true, null, normalizedExtension);

    public static ImageValidationResult Failure(string errorMessage) =>
        new(false, errorMessage, null);
}

public sealed class ProductImageValidator
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] RiffSignature = [0x52, 0x49, 0x46, 0x46]; // "RIFF"
    private static readonly byte[] WebpSignature = [0x57, 0x45, 0x42, 0x50]; // "WEBP"

    public ImageValidationResult Validate(IFormFile? file, long maxFileSizeBytes)
    {
        if (file is null || file.Length == 0)
        {
            return ImageValidationResult.Failure("Image file is empty or not provided.");
        }

        if (file.Length > maxFileSizeBytes)
        {
            return ImageValidationResult.Failure(
                $"Image file size ({file.Length} bytes) exceeds the maximum allowed limit of {maxFileSizeBytes} bytes (5 MiB).");
        }

        using var stream = file.OpenReadStream();
        return ValidateStream(stream, file.FileName, file.ContentType, file.Length, maxFileSizeBytes);
    }

    public ImageValidationResult ValidateStream(
        Stream stream,
        string fileName,
        string contentType,
        long fileLength,
        long maxFileSizeBytes)
    {
        if (stream is null || fileLength == 0)
        {
            return ImageValidationResult.Failure("Image file is empty or not provided.");
        }

        if (fileLength > maxFileSizeBytes)
        {
            return ImageValidationResult.Failure(
                $"Image file size ({fileLength} bytes) exceeds the maximum allowed limit of {maxFileSizeBytes} bytes (5 MiB).");
        }

        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ImageValidationResult.Failure("Image file must have an extension.");
        }

        var normalizedContentType = contentType?.Trim().ToLowerInvariant();

        string normalizedExtension;
        switch (extension)
        {
            case ".jpg":
            case ".jpeg":
                if (normalizedContentType != "image/jpeg")
                {
                    return ImageValidationResult.Failure(
                        $"Content-Type '{contentType}' is not valid for JPEG image.");
                }
                normalizedExtension = ".jpg";
                break;

            case ".png":
                if (normalizedContentType != "image/png")
                {
                    return ImageValidationResult.Failure(
                        $"Content-Type '{contentType}' is not valid for PNG image.");
                }
                normalizedExtension = ".png";
                break;

            case ".webp":
                if (normalizedContentType != "image/webp")
                {
                    return ImageValidationResult.Failure(
                        $"Content-Type '{contentType}' is not valid for WEBP image.");
                }
                normalizedExtension = ".webp";
                break;

            default:
                return ImageValidationResult.Failure(
                    $"Unsupported image format '{extension}'. Only JPEG (.jpg, .jpeg), PNG (.png), and WEBP (.webp) are supported.");
        }

        // Validate magic bytes
        byte[] header = new byte[16];
        var position = stream.CanSeek ? stream.Position : 0;
        int bytesRead = stream.Read(header, 0, header.Length);
        if (stream.CanSeek)
        {
            stream.Position = position;
        }

        if (!MatchesSignature(header, bytesRead, normalizedExtension))
        {
            return ImageValidationResult.Failure(
                "File content does not match the expected image signature.");
        }

        return ImageValidationResult.Success(normalizedExtension);
    }

    private static bool MatchesSignature(byte[] header, int bytesRead, string normalizedExtension)
    {
        return normalizedExtension switch
        {
            ".png" => bytesRead >= PngSignature.Length &&
                      header.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature),

            ".jpg" => bytesRead >= JpegSignature.Length &&
                      header.AsSpan(0, JpegSignature.Length).SequenceEqual(JpegSignature),

            ".webp" => bytesRead >= 12 &&
                       header.AsSpan(0, 4).SequenceEqual(RiffSignature) &&
                       header.AsSpan(8, 4).SequenceEqual(WebpSignature),

            _ => false
        };
    }
}
