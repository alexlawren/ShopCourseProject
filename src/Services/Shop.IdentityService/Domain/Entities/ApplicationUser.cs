using Microsoft.AspNetCore.Identity;

namespace Shop.IdentityService.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
