using Microsoft.EntityFrameworkCore;
using Shop.OrderService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("OrderDb")
    ?? throw new InvalidOperationException(
        "Connection string 'OrderDb' is not configured. " +
        "Use user-secrets or environment variable 'ConnectionStrings__OrderDb'.");

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "OrderService" }));

app.Run();
