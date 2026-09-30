namespace Shop.IdentityService.Application.Auth.Dtos;

public sealed class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public UserDto User { get; set; } = null!;
}
