using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shop.Web.Models.Auth;

public class LoginModel
{
    [Required(ErrorMessage = "Электронная почта обязательна")]
    [EmailAddress(ErrorMessage = "Некорректный формат электронной почты")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Пароль обязателен")]
    [MinLength(6, ErrorMessage = "Пароль должен содержать минимум 6 символов")]
    public string Password { get; set; } = string.Empty;
}

public class RegisterModel
{
    [Required(ErrorMessage = "Электронная почта обязательна")]
    [EmailAddress(ErrorMessage = "Некорректный формат электронной почты")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Пароль обязателен")]
    [MinLength(8, ErrorMessage = "Пароль должен содержать минимум 8 символов")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])[\s\S]+$", ErrorMessage = "Добавьте строчную и заглавную латинские буквы и цифру.")]
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
            var allErrors = Errors.Select(e => LocalizeValidation(e.Key)).Distinct();
            var combined = string.Join("; ", allErrors);
            if (!string.IsNullOrWhiteSpace(combined))
                return combined;
        }

        // Only recognized public messages are displayed; raw downstream details may contain internals.
        if (Detail?.StartsWith("A category with slug '", StringComparison.Ordinal) == true &&
            Detail.EndsWith("' already exists.", StringComparison.Ordinal))
            return "Категория с таким Slug уже существует.";

        var knownMessage = (Detail ?? Title) switch
        {
            "Invalid email or password." => "Неверная электронная почта или пароль.",
            "Email already registered." => "Эта электронная почта уже зарегистрирована.",
            "Invalid or expired refresh token." => "Сессия истекла. Войдите снова.",
            _ => null
        };
        return knownMessage ?? GetStatusMessage(Status);
    }

    public static string GetStatusMessage(int? status) => status switch
    {
        400 => "Проверьте введённые данные.",
        401 => "Сессия истекла или требуется вход. Войдите в систему.",
        403 => "Доступ запрещён. Недостаточно прав.",
        404 => "Запрашиваемый ресурс не найден.",
        409 => "Данные изменились или уже существуют. Обновите страницу и повторите операцию.",
        413 => "Файл слишком большой. Максимальный размер изображения — 5 МБ.",
        415 => "Неподдерживаемый формат файла или запроса.",
        429 => "Слишком много запросов. Попробуйте немного позже.",
        >= 500 => "Сервис временно недоступен. Попробуйте позже.",
        _ => "Произошла ошибка при выполнении запроса."
    };

    private static string LocalizeValidation(string field) => field.Split('.').Last() switch
    {
        "PasswordTooShort" => "Пароль должен содержать минимум 8 символов.",
        "PasswordRequiresUpper" => "Добавьте заглавную латинскую букву в пароль.",
        "PasswordRequiresLower" => "Добавьте строчную латинскую букву в пароль.",
        "PasswordRequiresDigit" => "Добавьте цифру в пароль.",
        "DuplicateEmail" or "DuplicateUserName" => "Эта электронная почта уже зарегистрирована.",
        "Email" or "InvalidEmail" or "InvalidUserName" => "Проверьте электронную почту.",
        "Password" => "Пароль: минимум 8 символов, строчная и заглавная латинские буквы и цифра.",
        "Price" => "Цена должна быть неотрицательной и содержать не более двух знаков после запятой.",
        "StockQuantity" or "Quantity" => "Проверьте количество товара: требуется целое число в допустимом диапазоне.",
        "Slug" => "Slug должен содержать строчные латинские буквы, цифры и дефис.",
        "Name" => "Проверьте название: оно обязательно и не должно превышать допустимую длину.",
        "Description" => "Описание не должно превышать 4000 символов.",
        "CategoryId" => "Выберите доступную категорию товара.",
        _ => "Проверьте введённые данные."
    };

    public static string? MapCodeToMessage(string code) => code switch
    {
        "EMPTY_CART" => "Корзина пуста. Добавьте товары перед оформлением заказа.",
        "CART_CHANGED" => "Содержимое корзины изменилось во время оформления. Пожалуйста, проверьте корзину.",
        "OUT_OF_STOCK" => "Недостаточно товара на складе.",
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
        "INVALID_PAGE" or "INVALID_PAGE_SIZE" => "Некорректные параметры страницы.",
        "INVALID_STATUS" or "INVALID_PAYMENT_STATUS" => "Выберите допустимый статус.",
        _ => null
    };
}
