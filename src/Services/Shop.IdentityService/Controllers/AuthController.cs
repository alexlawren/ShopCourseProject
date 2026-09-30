using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shop.IdentityService.Application.Auth.Dtos;
using Shop.IdentityService.Application.Auth.Options;
using Shop.IdentityService.Application.Auth.Services;
using Shop.IdentityService.Domain.Constants;
using Shop.IdentityService.Domain.Entities;
using Shop.IdentityService.Infrastructure.Persistence;

namespace Shop.IdentityService.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly IdentityDbContext _db;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        IdentityDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
        _db = db;
    }

    // ---------------------------------------------------------------
    // POST /api/auth/register
    // ---------------------------------------------------------------
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Email already registered.",
            });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
                ModelState.AddModelError(error.Code, error.Description);
            return ValidationProblem();
        }

        await _userManager.AddToRoleAsync(user, AppRoles.Customer);

        var roles = await _userManager.GetRolesAsync(user);
        var authResponse = await CreateAuthResponseAsync(user, roles);

        return StatusCode(StatusCodes.Status201Created, authResponse);
    }

    // ---------------------------------------------------------------
    // POST /api/auth/login
    // ---------------------------------------------------------------
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid email or password.",
            });
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid email or password.",
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var authResponse = await CreateAuthResponseAsync(user, roles);

        return Ok(authResponse);
    }

    // ---------------------------------------------------------------
    // POST /api/auth/refresh
    // ---------------------------------------------------------------
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);

        var storedToken = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (storedToken is null || !storedToken.IsActive)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid or expired refresh token.",
            });
        }

        var user = storedToken.User;
        var roles = await _userManager.GetRolesAsync(user);

        // Revoke old token
        storedToken.RevokedAtUtc = DateTime.UtcNow;

        // Create new refresh token
        var newRawRefresh = _tokenService.GenerateRefreshToken();
        var newHash = _tokenService.HashToken(newRawRefresh);

        storedToken.ReplacedByTokenHash = newHash;

        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newHash,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
        };

        _db.RefreshTokens.Add(newRefreshToken);
        await _db.SaveChangesAsync();

        var accessToken = _tokenService.CreateAccessToken(user, roles);

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRawRefresh,
            AccessTokenExpiresAtUtc = _tokenService.GetAccessTokenExpiry(),
            User = MapToDto(user, roles),
        });
    }

    // ---------------------------------------------------------------
    // POST /api/auth/logout
    // ---------------------------------------------------------------
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);

        var storedToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (storedToken is not null && storedToken.RevokedAtUtc is null)
        {
            storedToken.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    // ---------------------------------------------------------------
    // GET /api/auth/me
    // ---------------------------------------------------------------
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (userId is null || !Guid.TryParse(userId, out var id))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(MapToDto(user, roles));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------
    private async Task<AuthResponse> CreateAuthResponseAsync(
        ApplicationUser user, IList<string> roles)
    {
        var rawRefresh = _tokenService.GenerateRefreshToken();
        var refreshHash = _tokenService.HashToken(rawRefresh);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        var accessToken = _tokenService.CreateAccessToken(user, roles);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefresh,
            AccessTokenExpiresAtUtc = _tokenService.GetAccessTokenExpiry(),
            User = MapToDto(user, roles),
        };
    }

    private static UserDto MapToDto(ApplicationUser user, IList<string> roles)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email!,
            Roles = roles.ToList().AsReadOnly(),
        };
    }
}
