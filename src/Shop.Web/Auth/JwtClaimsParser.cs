using System.Security.Claims;
using System.Text.Json;

namespace Shop.Web.Auth;

public static class JwtClaimsParser
{
    public static IEnumerable<Claim> ParseClaimsFromJwt(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
            return [];

        var parts = jwt.Split('.');
        if (parts.Length < 2)
            return [];

        var payload = parts[1];
        byte[] jsonBytes;
        try
        {
            jsonBytes = ParseBase64WithoutPadding(payload);
        }
        catch
        {
            return [];
        }

        try
        {
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
            if (keyValuePairs == null)
                return [];

            var claims = new List<Claim>();

            foreach (var kvp in keyValuePairs)
            {
                if (kvp.Key == "role" || kvp.Key == "roles" || kvp.Key == ClaimTypes.Role)
                {
                    if (kvp.Value is JsonElement element && element.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in element.EnumerateArray())
                        {
                            var role = item.GetString();
                            if (!string.IsNullOrWhiteSpace(role))
                            {
                                claims.Add(new Claim(ClaimTypes.Role, role));
                            }
                        }
                    }
                    else
                    {
                        var role = kvp.Value?.ToString();
                        if (!string.IsNullOrWhiteSpace(role))
                        {
                            claims.Add(new Claim(ClaimTypes.Role, role));
                        }
                    }
                }
                else if (kvp.Key == "email" || kvp.Key == ClaimTypes.Email)
                {
                    var email = kvp.Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        claims.Add(new Claim(ClaimTypes.Email, email));
                        claims.Add(new Claim(ClaimTypes.Name, email));
                    }
                }
                else if (kvp.Key == "sub" || kvp.Key == ClaimTypes.NameIdentifier)
                {
                    var sub = kvp.Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(sub))
                    {
                        claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
                    }
                }
                else
                {
                    var val = kvp.Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        claims.Add(new Claim(kvp.Key, val));
                    }
                }
            }

            return claims;
        }
        catch
        {
            return [];
        }
    }

    public static byte[] ParseBase64WithoutPadding(string base64)
    {
        base64 = base64.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }

    public static long? GetExpirationUnixTime(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
            return null;

        var parts = jwt.Split('.');
        if (parts.Length < 2)
            return null;

        try
        {
            var jsonBytes = ParseBase64WithoutPadding(parts[1]);
            using var doc = JsonDocument.Parse(jsonBytes);
            if (doc.RootElement.TryGetProperty("exp", out var expElement))
            {
                if (expElement.ValueKind == JsonValueKind.Number && expElement.TryGetInt64(out var exp))
                {
                    return exp;
                }

                if (expElement.ValueKind == JsonValueKind.String && long.TryParse(expElement.GetString(), out var expParsed))
                {
                    return expParsed;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public static DateTimeOffset? GetExpiration(string? jwt)
    {
        var exp = GetExpirationUnixTime(jwt);
        return exp.HasValue ? DateTimeOffset.FromUnixTimeSeconds(exp.Value) : null;
    }

    public static bool IsExpiredOrExpiringSoon(string? jwt, TimeSpan? skew = null)
    {
        var expiration = GetExpiration(jwt);
        if (!expiration.HasValue)
            return true;

        var effectiveSkew = skew ?? TimeSpan.FromSeconds(30);
        return DateTimeOffset.UtcNow + effectiveSkew >= expiration.Value;
    }
}

