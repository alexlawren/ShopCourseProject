using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shop.IdentityService.Application.Auth.Options;
using Shop.IdentityService.Application.Auth.Services;
using Shop.IdentityService.Domain.Entities;
using Shop.IdentityService.Infrastructure.Identity;
using Shop.IdentityService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// --------------- Database ---------------
var connectionString = builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException(
        "Connection string 'IdentityDb' is not configured. " +
        "Use user-secrets or environment variable 'ConnectionStrings__IdentityDb'.");

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(connectionString));

// --------------- ASP.NET Core Identity ---------------
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = false;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<IdentityDbContext>()
.AddDefaultTokenProviders();

// --------------- JWT ---------------
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);

var jwtKey = jwtSection["Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException(
        "Jwt:Key is not configured. " +
        "Set it via user-secrets or environment variable 'Jwt__Key'.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromSeconds(30),
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
    };
});

builder.Services.AddAuthorization();

// --------------- Application Services ---------------
builder.Services.AddScoped<ITokenService, TokenService>();

// --------------- Controllers ---------------
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

var app = builder.Build();

// --------------- Middleware ---------------
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "IdentityService" }));

// --------------- Apply EF Migrations & Seed roles ---------------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    const int maxRetries = 10;
    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            logger.LogInformation("Applying Identity EF migrations (attempt {Attempt}/{MaxRetries})...", attempt, maxRetries);
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Identity EF migrations applied successfully.");
            break;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            logger.LogWarning(ex, "Failed to apply Identity EF migrations on attempt {Attempt}. Retrying in 2 seconds...", attempt);
            await Task.Delay(2000);
        }
    }
}

await IdentityDataSeeder.SeedAsync(app.Services);

app.Run();

public partial class Program;

