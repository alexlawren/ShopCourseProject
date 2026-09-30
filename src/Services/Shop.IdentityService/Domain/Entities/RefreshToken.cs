namespace Shop.IdentityService.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// SHA-256 hash of the raw refresh token. Raw token is never stored.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>
    /// Hash of the replacement token created during rotation.
    /// </summary>
    public string? ReplacedByTokenHash { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
