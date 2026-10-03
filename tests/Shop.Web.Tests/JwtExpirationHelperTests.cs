using System.Text;
using System.Text.Json;
using Shop.Web.Auth;

namespace Shop.Web.Tests;

public class JwtExpirationHelperTests
{
    private static string CreateTestJwt(long? exp)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var payloadDict = new Dictionary<string, object>();
        if (exp.HasValue)
        {
            payloadDict["exp"] = exp.Value;
        }

        var payloadJson = JsonSerializer.Serialize(payloadDict);
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var signature = "mock_sig";
        return $"{header}.{payloadBase64}.{signature}";
    }

    [Fact]
    public void IsExpiredOrExpiringSoon_FutureTokenBeyondSkew_ReturnsFalse()
    {
        // Token expires in 10 minutes (600 seconds)
        var futureExp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var jwt = CreateTestJwt(futureExp);

        var result = JwtClaimsParser.IsExpiredOrExpiringSoon(jwt);

        Assert.False(result);
    }

    [Fact]
    public void IsExpiredOrExpiringSoon_TokenExpiringWithinDefaultSkew_ReturnsTrue()
    {
        // Token expires in 15 seconds (less than default 30s skew)
        var expSoon = DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeSeconds();
        var jwt = CreateTestJwt(expSoon);

        var result = JwtClaimsParser.IsExpiredOrExpiringSoon(jwt);

        Assert.True(result);
    }

    [Fact]
    public void IsExpiredOrExpiringSoon_AlreadyExpiredToken_ReturnsTrue()
    {
        // Token expired 10 minutes ago
        var pastExp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var jwt = CreateTestJwt(pastExp);

        var result = JwtClaimsParser.IsExpiredOrExpiringSoon(jwt);

        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid.jwt")]
    [InlineData("header.bad_base64.sig")]
    public void IsExpiredOrExpiringSoon_MalformedOrNullToken_ReturnsTrue(string? token)
    {
        var result = JwtClaimsParser.IsExpiredOrExpiringSoon(token);
        Assert.True(result);
    }

    [Fact]
    public void IsExpiredOrExpiringSoon_TokenWithoutExpClaim_ReturnsTrue()
    {
        var jwt = CreateTestJwt(null);
        var result = JwtClaimsParser.IsExpiredOrExpiringSoon(jwt);
        Assert.True(result);
    }

    [Fact]
    public void IsExpiredOrExpiringSoon_CustomSkewRespected()
    {
        // Expires in 45 seconds
        var exp = DateTimeOffset.UtcNow.AddSeconds(45).ToUnixTimeSeconds();
        var jwt = CreateTestJwt(exp);

        // Skew of 30 seconds -> expires in 45s > 30s -> false
        Assert.False(JwtClaimsParser.IsExpiredOrExpiringSoon(jwt, TimeSpan.FromSeconds(30)));

        // Skew of 60 seconds -> expires in 45s < 60s -> true
        Assert.True(JwtClaimsParser.IsExpiredOrExpiringSoon(jwt, TimeSpan.FromSeconds(60)));
    }
}
