using Microsoft.EntityFrameworkCore;
using Shop.IdentityService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException(
        "Connection string 'IdentityDb' is not configured. " +
        "Use user-secrets or environment variable 'ConnectionStrings__IdentityDb'.");

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "IdentityService" }));

app.Run();
