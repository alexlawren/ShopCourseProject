using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shop.CatalogService.Application.Catalog.Images;
using Shop.CatalogService.Application.Catalog.Services;
using Shop.CatalogService.Application.StockReservation.Services;
using Shop.CatalogService.Grpc.Services;
using Shop.CatalogService.Infrastructure.Persistence;
using Shop.CatalogService.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// --------------- Database ---------------
var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException(
        "Connection string 'CatalogDb' is not configured. " +
        "Use user-secrets or environment variable 'ConnectionStrings__CatalogDb'.");

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(connectionString));

// --------------- JWT Authentication & Authorization ---------------
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = builder.Configuration["Jwt:Key"] ?? jwtSection["Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "Jwt:Key is not configured. " +
        "Set it via user-secrets or environment variable 'Jwt__Key'.");
}

var issuer = jwtSection["Issuer"] ?? "ShopCourseProject.Identity";
var audience = jwtSection["Audience"] ?? "ShopCourseProject.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

// --------------- Application Services ---------------
builder.Services.AddScoped<ICatalogQueryService, CatalogQueryService>();
builder.Services.AddScoped<ICatalogCommandService, CatalogCommandService>();
builder.Services.AddScoped<IStockReservationService, StockReservationService>();
builder.Services.AddScoped<Shop.CatalogService.Application.Catalog.Notifications.ICatalogNotificationService, Shop.CatalogService.Infrastructure.Notifications.SignalRCatalogNotificationService>();

// --------------- SignalR ---------------
builder.Services.AddSignalR();

// --------------- gRPC Services ---------------
builder.Services.AddGrpc();

// --------------- Product Image Storage & Service ---------------
builder.Services.Configure<ProductImageOptions>(
    builder.Configuration.GetSection(ProductImageOptions.SectionName));
builder.Services.AddSingleton<ProductImageValidator>();
builder.Services.AddSingleton<IProductImageStorage, LocalProductImageStorage>();
builder.Services.AddScoped<IProductImageService, ProductImageService>();

// --------------- Controllers & Error Handling ---------------
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

var app = builder.Build();

// --------------- Middleware ---------------
app.UseExceptionHandler();

// --------------- Static File Serving (Product Images) ---------------
var imageOptions = app.Configuration
    .GetSection(ProductImageOptions.SectionName)
    .Get<ProductImageOptions>() ?? new ProductImageOptions();

var storageDirectory = Path.IsPathRooted(imageOptions.StoragePath)
    ? Path.GetFullPath(imageOptions.StoragePath)
    : Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, imageOptions.StoragePath));

Directory.CreateDirectory(storageDirectory);

var requestPath = "/" + (imageOptions.RequestPath ?? "product-images").Trim('/');

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(storageDirectory),
    RequestPath = requestPath
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<Shop.CatalogService.Hubs.CatalogHub>("/hubs/catalog");
app.MapGrpcService<StockReservationGrpcService>();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "CatalogService" }));

// --------------- Apply EF Migrations ---------------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    const int maxRetries = 10;
    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            logger.LogInformation("Applying Catalog EF migrations (attempt {Attempt}/{MaxRetries})...", attempt, maxRetries);
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Catalog EF migrations applied successfully.");
            break;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            logger.LogWarning(ex, "Failed to apply Catalog EF migrations on attempt {Attempt}. Retrying in 2 seconds...", attempt);
            await Task.Delay(2000);
        }
    }
}

app.Run();

public partial class Program;

