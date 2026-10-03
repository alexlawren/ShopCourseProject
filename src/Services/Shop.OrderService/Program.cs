using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shop.CatalogService.Grpc;
using Shop.OrderService.Application.Cart.Services;
using Shop.OrderService.Application.Orders.Services;
using Shop.OrderService.Infrastructure.Grpc;
using Shop.OrderService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// --------------- Database ---------------
var connectionString = builder.Configuration.GetConnectionString("OrderDb")
    ?? throw new InvalidOperationException(
        "Connection string 'OrderDb' is not configured. " +
        "Use user-secrets or environment variable 'ConnectionStrings__OrderDb'.");

builder.Services.AddDbContext<OrderDbContext>(options =>
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

// --------------- gRPC Clients ---------------
var catalogGrpcAddress = builder.Configuration["CatalogGrpc:Address"] ?? "http://localhost:5059";
builder.Services.AddGrpcClient<StockReservationService.StockReservationServiceClient>(options =>
{
    options.Address = new Uri(catalogGrpcAddress);
});

// --------------- Application Services ---------------
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICatalogStockClient, CatalogStockGrpcClient>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IOrderQueryService, OrderQueryService>();
builder.Services.AddScoped<IOrderManagementService, OrderManagementService>();


// --------------- Controllers & Error Handling ---------------
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

var app = builder.Build();

// --------------- Middleware ---------------
app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "OrderService" }));

app.Run();

public partial class Program;
