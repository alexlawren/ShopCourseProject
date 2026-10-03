using System.Text.RegularExpressions;

namespace Shop.Web.Services;

public static partial class AdminCatalogValidatorHelper
{
    private static readonly Regex SlugRegex = MySlugRegex();

    public const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MiB

    public static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public static readonly string[] AllowedImageMimeTypes = ["image/jpeg", "image/png", "image/webp"];

    public static bool IsValidSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 140)
        {
            return false;
        }

        return SlugRegex.IsMatch(slug);
    }

    public static bool ValidateImage(string fileName, string contentType, long size, out string? errorMessage)
    {
        if (size <= 0)
        {
            errorMessage = "Файл изображения пуст.";
            return false;
        }

        if (size > MaxImageSizeBytes)
        {
            errorMessage = "Размер изображения не должен превышать 5 МБ.";
            return false;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
        {
            errorMessage = $"Недопустимое расширение файла '{ext}'. Разрешены только: {string.Join(", ", AllowedImageExtensions)}.";
            return false;
        }

        if (!AllowedImageMimeTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            errorMessage = $"Недопустимый тип содержимого '{contentType}'. Разрешены только: {string.Join(", ", AllowedImageMimeTypes)}.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public static bool IsValidPrice(decimal price) => price >= 0;

    public static bool IsValidStock(int stockQuantity) => stockQuantity >= 0;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled)]
    private static partial Regex MySlugRegex();
}
