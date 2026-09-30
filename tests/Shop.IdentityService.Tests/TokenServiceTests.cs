using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Shop.IdentityService.Application.Auth.Options;
using Shop.IdentityService.Application.Auth.Services;
using Shop.IdentityService.Domain.Entities;

namespace Shop.IdentityService.Tests;

public class TokenServiceTests
{
    private readonly TokenService _sut;
    private readonly JwtOptions _jwtOptions;

    public TokenServiceTests()
    {
        _jwtOptions = new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            Key = "ThisIsATestKeyThatIsAtLeast32BytesLong!!", // 42 chars
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
        };
        _sut = new TokenService(Options.Create(_jwtOptions));
    }

    private static ApplicationUser CreateTestUser() => new()
    {
        Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        Email = "test@example.com",
        UserName = "test@example.com",
    };

    // ---------------------------------------------------------------
    // JWT access token tests
    // ---------------------------------------------------------------

    [Fact]
    public void CreateAccessToken_ContainsCorrectSub()
    {
        var user = CreateTestUser();
        var token = _sut.CreateAccessToken(user, ["Customer"]);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal(user.Id.ToString(), jwt.Subject);
    }

    [Fact]
    public void CreateAccessToken_ContainsEmail()
    {
        var user = CreateTestUser();
        var token = _sut.CreateAccessToken(user, ["Customer"]);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var emailClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email);
        Assert.NotNull(emailClaim);
        Assert.Equal("test@example.com", emailClaim.Value);
    }

    [Fact]
    public void CreateAccessToken_ContainsRoleClaims()
    {
        var user = CreateTestUser();
        var token = _sut.CreateAccessToken(user, ["Customer", "Admin"]);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var roles = jwt.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        Assert.Contains("Customer", roles);
        Assert.Contains("Admin", roles);
    }

    [Fact]
    public void CreateAccessToken_HasExpiration()
    {
        var user = CreateTestUser();
        var token = _sut.CreateAccessToken(user, ["Customer"]);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.True(jwt.ValidTo > DateTime.UtcNow);
        Assert.True(jwt.ValidTo <= DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes + 1));
    }

    // ---------------------------------------------------------------
    // Refresh token tests
    // ---------------------------------------------------------------

    [Fact]
    public void HashToken_SameInput_SameOutput()
    {
        var raw = "some-refresh-token-value";
        var hash1 = _sut.HashToken(raw);
        var hash2 = _sut.HashToken(raw);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashToken_DiffersFromRawToken()
    {
        var raw = "some-refresh-token-value";
        var hash = _sut.HashToken(raw);

        Assert.NotEqual(raw, hash);
    }

    [Fact]
    public void GenerateRefreshToken_TwoTokensAreDifferent()
    {
        var token1 = _sut.GenerateRefreshToken();
        var token2 = _sut.GenerateRefreshToken();

        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void GenerateRefreshToken_IsNotEmpty()
    {
        var token = _sut.GenerateRefreshToken();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(token.Length > 20); // Base64 of 64 bytes ≈ 88 chars
    }
}
