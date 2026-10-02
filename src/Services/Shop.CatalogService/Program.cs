using Microsoft.EntityFrameworkCore;
using Shop.CatalogService.Application.Catalog.Services;
using Shop.CatalogService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// --------------- Database ---------------
var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException(
        "Connection string 'CatalogDb' is not configured. " +
        "Use user-secrets or environment variable 'ConnectionStrings__CatalogDb'.");

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(connectionString));

// --------------- Application Services ---------------
builder.Services.AddScoped<ICatalogQueryService, CatalogQueryService>();

// --------------- Controllers & Error Handling ---------------
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

var app = builder.Build();

// --------------- Middleware ---------------
app.UseExceptionHandler();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "CatalogService" }));

app.Run();
