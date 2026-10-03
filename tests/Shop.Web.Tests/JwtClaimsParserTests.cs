using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Shop.Web.Auth;

namespace Shop.Web.Tests;

public class JwtClaimsParserTests
{
    private static string CreateTestJwt(Dictionary<string, object> payload)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var signature = "mock_signature";
        return $"{header}.{payloadBase64}.{signature}";
    }

    [Fact]
    public void ParseClaimsFromJwt_ValidToken_ReturnsExpectedClaims()
    {
        var userId = Guid.NewGuid().ToString();
        var email = "customer@shop.local";
        var payload = new Dictionary<string, object>
        {
            { "sub", userId },
            { "email", email },
            { "role", "Customer" }
        };

        var jwt = CreateTestJwt(payload);
        var claims = JwtClaimsParser.ParseClaimsFromJwt(jwt).ToList();

        Assert.NotEmpty(claims);
        Assert.Contains(claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == userId);
        Assert.Contains(claims, c => c.Type == ClaimTypes.Email && c.Value == email);
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Customer");
    }

    [Fact]
    public void ParseClaimsFromJwt_MultipleRoles_ReturnsAllRoles()
    {
        var payload = new Dictionary<string, object>
        {
            { "sub", "user-123" },
            { "email", "admin@shop.local" },
            { "role", new[] { "Admin", "Customer" } }
        };

        var jwt = CreateTestJwt(payload);
        var claims = JwtClaimsParser.ParseClaimsFromJwt(jwt).ToList();

        var roleClaims = claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains("Admin", roleClaims);
        Assert.Contains("Customer", roleClaims);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("invalid-jwt")]
    [InlineData("part1.part2")]
    [InlineData("part1.part2.part3.part4")]
    public void ParseClaimsFromJwt_MalformedToken_ReturnsEmptyClaims(string? malformedJwt)
    {
        var claims = JwtClaimsParser.ParseClaimsFromJwt(malformedJwt!);
        Assert.Empty(claims);
    }

    [Fact]
    public void ParseClaimsFromJwt_InvalidBase64UrlPayload_ReturnsEmptyClaims()
    {
        var invalidJwt = "header.!!!invalid-base64!!!.sig";
        var claims = JwtClaimsParser.ParseClaimsFromJwt(invalidJwt);
        Assert.Empty(claims);
    }
}
