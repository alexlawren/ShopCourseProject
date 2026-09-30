using System.ComponentModel.DataAnnotations;

namespace Shop.IdentityService.Application.Auth.Dtos;

public sealed class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
