using Shop.IdentityService.Domain.Entities;

namespace Shop.IdentityService.Tests;

public class RefreshTokenTests
{
    [Fact]
    public void IsActive_WhenNotRevokedAndNotExpired_ReturnsTrue()
    {
        var token = new RefreshToken
        {
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            RevokedAtUtc = null,
        };

        Assert.True(token.IsActive);
    }

    [Fact]
    public void IsActive_WhenRevoked_ReturnsFalse()
    {
        var token = new RefreshToken
        {
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            RevokedAtUtc = DateTime.UtcNow,
        };

        Assert.False(token.IsActive);
    }

    [Fact]
    public void IsActive_WhenExpired_ReturnsFalse()
    {
        var token = new RefreshToken
        {
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
            RevokedAtUtc = null,
        };

        Assert.False(token.IsActive);
    }
}
