using System.Security.Claims;
using Identity.Api.Controllers;
using Identity.Api.Data;
using Identity.Api.Domain;
using Identity.Api.Dtos;
using Identity.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SneakerShop.BuildingBlocks;
using SneakerShop.BuildingBlocks.Caching;
using Xunit;

namespace Identity.Api.Tests;

public sealed class AuthControllerTests : IDisposable
{
    private readonly AuthDbContext _db;
    private readonly AuthController _controller;
    private readonly FakeLoginAttemptLimiter _loginAttempts = new();
    private readonly FakeRevocationStore _revocations = new();

    public AuthControllerTests()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AuthDbContext(options);
        var tokens = new TokenService(Options.Create(new JwtOptions
        {
            Key = "this-is-a-test-secret-key-1234567890",
            Issuer = "SneakerShop",
            Audience = "SneakerShop-Clients",
            ExpiresMinutes = 60
        }));

        _controller = new AuthController(_db, tokens, _loginAttempts, _revocations);
    }

    [Fact]
    public async Task Login_IsRejectedWhileEmailIsLockedEvenWithCorrectPassword()
    {
        await AddCustomer("locked@example.com", "Password123!");
        _loginAttempts.Lockout = TimeSpan.FromSeconds(90);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _controller.Login(new LoginRequest { Email = "Locked@Example.com", Password = "Password123!" }, CancellationToken.None));

        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
        Assert.Contains("2 minute(s)", exception.Message);
    }

    [Fact]
    public async Task Login_RecordsFailuresAndResetsThemAfterSuccess()
    {
        await AddCustomer("retry@example.com", "Password123!");

        await Assert.ThrowsAsync<AppException>(() =>
            _controller.Login(new LoginRequest { Email = "retry@example.com", Password = "nope" }, CancellationToken.None));
        Assert.Equal(new[] { "retry@example.com" }, _loginAttempts.Failures);

        await _controller.Login(new LoginRequest { Email = "retry@example.com", Password = "Password123!" }, CancellationToken.None);
        Assert.Equal(new[] { "retry@example.com" }, _loginAttempts.Resets);
    }

    [Fact]
    public async Task Logout_RevokesTokenUntilItExpires()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("jti", "token-1"),
                    new Claim("exp", expiresAt.ToUnixTimeSeconds().ToString())
                }, "TestAuth"))
            }
        };

        var result = await _controller.Logout();

        Assert.IsType<NoContentResult>(result);
        var (jti, revokedUntil) = Assert.Single(_revocations.Revoked);
        Assert.Equal("token-1", jti);
        Assert.Equal(expiresAt.ToUnixTimeSeconds(), new DateTimeOffset(revokedUntil).ToUnixTimeSeconds());
    }

    private async Task AddCustomer(string email, string password)
    {
        _db.Users.Add(new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = "Customer",
            Role = Roles.Customer
        });
        await _db.SaveChangesAsync();
    }

    private sealed class FakeLoginAttemptLimiter : ILoginAttemptLimiter
    {
        public TimeSpan? Lockout { get; set; }
        public List<string> Failures { get; } = [];
        public List<string> Resets { get; } = [];

        public Task<TimeSpan?> GetLockoutAsync(string email) => Task.FromResult(Lockout);

        public Task RecordFailureAsync(string email)
        {
            Failures.Add(email);
            return Task.CompletedTask;
        }

        public Task ResetAsync(string email)
        {
            Resets.Add(email);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRevocationStore : ITokenRevocationStore
    {
        public List<(string Jti, DateTime ExpiresAtUtc)> Revoked { get; } = [];

        public Task RevokeAsync(string jti, DateTime expiresAtUtc)
        {
            Revoked.Add((jti, expiresAtUtc));
            return Task.CompletedTask;
        }

        public Task<bool> IsRevokedAsync(string jti) => Task.FromResult(Revoked.Any(r => r.Jti == jti));
    }

    [Fact]
    public async Task Register_CreatesCustomerAccountAndJwt()
    {
        var result = await _controller.Register(new RegisterRequest
        {
            Email = "  jane@example.com ",
            Password = "Password123!",
            FullName = " Jane Doe ",
            Phone = " 123456789 "
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var auth = Assert.IsType<AuthResponse>(ok.Value);

        Assert.Equal(Roles.Customer, auth.User.Role);
        Assert.Equal("jane@example.com", auth.User.Email);
        Assert.Equal("Jane Doe", auth.User.FullName);
        Assert.Equal("123456789", auth.User.Phone);
        Assert.True(auth.AccessToken.Length > 0);
        Assert.True(auth.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_RejectsUnknownOrWrongCredentialsWithTheSameError()
    {
        _db.Users.Add(new User
        {
            Email = "customer@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            FullName = "Customer User",
            Role = Roles.Customer
        });
        await _db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _controller.Login(new LoginRequest
            {
                Email = "customer@example.com",
                Password = "WrongPassword!"
            }, CancellationToken.None));

        Assert.Equal(StatusCodes.Status401Unauthorized, exception.StatusCode);
        Assert.Equal("Invalid email or password.", exception.Message);
    }

    [Fact]
    public async Task Me_ReturnsAuthenticatedUserProfile()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "me@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            FullName = "Me User",
            Role = Roles.Admin
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("sub", user.Id.ToString())
                }, "TestAuth"))
            }
        };

        var result = await _controller.Me(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<UserDto>(ok.Value);
        Assert.Equal(user.Id, dto.Id);
        Assert.Equal(user.Email, dto.Email);
        Assert.Equal(user.Role, dto.Role);
    }

    public void Dispose() => _db.Dispose();
}
