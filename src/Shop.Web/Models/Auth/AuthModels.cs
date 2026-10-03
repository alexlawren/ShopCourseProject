using System.ComponentModel.DataAnnotations;

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
    public Dictionary<string, string[]>? Errors { get; set; }

    public string GetDisplayMessage()
    {
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
}
