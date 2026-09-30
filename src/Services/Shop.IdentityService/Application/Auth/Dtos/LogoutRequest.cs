using System.ComponentModel.DataAnnotations;

namespace Shop.IdentityService.Application.Auth.Dtos;

public sealed class LogoutRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
