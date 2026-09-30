using Microsoft.EntityFrameworkCore;
using Shop.CatalogService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException(
        "Connection string 'CatalogDb' is not configured. " +
        "Use user-secrets or environment variable 'ConnectionStrings__CatalogDb'.");

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "CatalogService" }));

app.Run();
