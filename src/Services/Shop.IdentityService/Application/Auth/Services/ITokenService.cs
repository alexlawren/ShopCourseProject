using Shop.IdentityService.Domain.Entities;

namespace Shop.IdentityService.Application.Auth.Services;

public interface ITokenService
{
    string CreateAccessToken(ApplicationUser user, IList<string> roles);
    DateTime GetAccessTokenExpiry();
    string GenerateRefreshToken();
    string HashToken(string rawToken);
}
