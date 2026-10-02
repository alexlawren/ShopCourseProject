using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Shop.OrderService.Controllers.Common;

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal user, out Guid userId)
    {
        var claim = user.FindFirst(JwtRegisteredClaimNames.Sub)
                 ?? user.FindFirst(ClaimTypes.NameIdentifier)
                 ?? user.FindFirst("sub");

        if (claim != null && Guid.TryParse(claim.Value, out userId))
        {
            return true;
        }

        userId = Guid.Empty;
        return false;
    }
}
