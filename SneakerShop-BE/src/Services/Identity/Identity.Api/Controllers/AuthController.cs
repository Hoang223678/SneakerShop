using Identity.Api.Data;
using Identity.Api.Domain;
using Identity.Api.Dtos;
using Identity.Api.Interfaces;
using Identity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SneakerShop.BuildingBlocks;
using SneakerShop.BuildingBlocks.Caching;

namespace Identity.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AuthDbContext db,
    ITokenService tokens,
    ITokenRevocationStore revocations) : ControllerBase, IAuthController
{
    /// <summary>Register a new customer account.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new AppException(StatusCodes.Status409Conflict, "This email is already registered.");

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            Phone = request.Phone?.Trim(),
            Role = Roles.Customer // public registration can never create admins
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Ok(BuildResponse(user));
    }

    /// <summary>Exchange email + password for a JWT access token.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Same message for unknown email and wrong password to avoid account enumeration.
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new AppException(StatusCodes.Status401Unauthorized, "Invalid email or password.");

        if (!user.IsActive)
            throw new AppException(StatusCodes.Status403Forbidden, "This account is disabled.");

        return Ok(BuildResponse(user));
    }

    /// <summary>Revoke the current access token. Every service rejects it from now until it would have expired.</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var jti = User.FindFirst("jti")?.Value;
        if (jti is null || !long.TryParse(User.FindFirst("exp")?.Value, out var expiresAtUnix))
            throw new AppException(StatusCodes.Status401Unauthorized, "Invalid access token.");

        try
        {
            await revocations.RevokeAsync(jti, DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix).UtcDateTime);
        }
        catch (Exception exception) when (exception.IsRedisFailure())
        {
            throw new AppException(StatusCodes.Status503ServiceUnavailable, "Logout is temporarily unavailable. Please try again.");
        }

        return NoContent();
    }

    /// <summary>Profile of the authenticated user.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var id = User.GetUserId();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? NotFound() : Ok(ToDto(user));
    }

    private AuthResponse BuildResponse(User user)
    {
        var (token, expires) = tokens.Create(user);
        return new AuthResponse(token, expires, ToDto(user));
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.FullName, u.Phone, u.Role);
}
