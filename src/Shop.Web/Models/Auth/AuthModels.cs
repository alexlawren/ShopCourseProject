using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shop.Web.Models.Auth;

public class LoginModel
{
    [Required(ErrorMessage = "Email обязателен")]
    [EmailAddress(ErrorMessage = "Некорректный формат email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Пароль обязателен")]
    [MinLength(6, ErrorMessage = "Пароль должен содержать минимум 6 символов")]
    public string Password { get; set; } = string.Empty;
}

public class RegisterModel
{
    [Required(ErrorMessage = "Email обязателен")]
    [EmailAddress(ErrorMessage = "Некорректный формат email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Пароль обязателен")]
    [MinLength(6, ErrorMessage = "Пароль должен содержать минимум 6 символов")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Подтверждение пароля обязательно")]
    [Compare(nameof(Password), ErrorMessage = "Пароли не совпадают")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class UserDtoModel
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = [];
}

public class AuthResponseModel
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public UserDtoModel User { get; set; } = null!;
}

public class RefreshTokenRequestModel
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequestModel
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class ProblemDetailsModel
{
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public int? Status { get; set; }
    public string? Code { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    public string? GetErrorCode()
    {
        if (!string.IsNullOrWhiteSpace(Code))
            return Code;

        if (ExtensionData != null)
        {
            if (ExtensionData.TryGetValue("code", out var codeElem) || ExtensionData.TryGetValue("Code", out codeElem))
            {
                if (codeElem.ValueKind == JsonValueKind.String)
                {
                    return codeElem.GetString();
                }
            }
        }

        return null;
    }

    public string GetDisplayMessage()
    {
        var code = GetErrorCode();
        if (!string.IsNullOrWhiteSpace(code))
        {
            var friendly = MapCodeToMessage(code);
            if (!string.IsNullOrWhiteSpace(friendly))
                return friendly;
        }

        if (Errors != null && Errors.Count > 0)
        {
            var allErrors = Errors.Values.SelectMany(v => v).Where(v => !string.IsNullOrWhiteSpace(v));
            var combined = string.Join("; ", allErrors);
            if (!string.IsNullOrWhiteSpace(combined))
                return combined;
        }

        if (!string.IsNullOrWhiteSpace(Detail))
            return Detail;

        if (!string.IsNullOrWhiteSpace(Title))
            return Title;

        return "Произошла ошибка при выполнении запроса.";
    }

    public static string? MapCodeToMessage(string code) => code switch
    {
        "EMPTY_CART" => "Корзина пуста. Добавьте товары перед оформлением заказа.",
        "CART_CHANGED" => "Содержимое корзины изменилось во время оформления. Пожалуйста, проверьте корзину.",
        "OUT_OF_STOCK" => "Недостаточно товара на складе для оформления заказа.",
        "PRODUCT_NOT_FOUND" => "Один или несколько товаров не найдены.",
        "PRODUCT_INACTIVE" => "Один из товаров в корзине снят с продажи.",
        "CATEGORY_INACTIVE" => "Категория одного из товаров в корзине неактивна.",
        "CATALOG_UNAVAILABLE" => "Сервис каталога временно недоступен. Пожалуйста, повторите попытку.",
        "ORDER_CANCELLATION_IN_PROGRESS" => "Отмена заказа уже выполняется. Повторите через несколько секунд.",
        "PAYMENT_CANCELLED" => "Заказ отменен, оплата невозможна.",
        "ORDER_CANNOT_BE_CANCELLED" => "Заказ не может быть отменен на текущей стадии.",
        "INVALID_ORDER_TRANSITION" => "Недопустимое изменение статуса заказа.",
        "DOWNSTREAM_ERROR" => "Внутренняя ошибка сервиса при обращении к каталогу. Пожалуйста, повторите попытку.",
        "REQUEST_ID_CONFLICT" => "Идентификатор запроса принадлежит другому заказу.",
        "INVALID_REQUEST_ID" => "Некорректный идентификатор запроса.",
        "CART_TOO_LARGE" => "В корзине не может быть более 100 позиций.",
        _ => null
    };
}
