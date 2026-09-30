namespace Shop.IdentityService.Domain.Constants;

public static class AppRoles
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Customer, Admin];
}
