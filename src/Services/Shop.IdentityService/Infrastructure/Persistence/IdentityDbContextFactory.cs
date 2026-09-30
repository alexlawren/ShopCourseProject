using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shop.IdentityService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Reads connection string from standard configuration (appsettings, user-secrets, env vars).
/// Does NOT require Jwt:Key or Identity services — only the DB connection.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<IdentityDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException(
                "Connection string 'IdentityDb' is not configured. " +
                "Set it via user-secrets or the 'ConnectionStrings__IdentityDb' environment variable.");

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
