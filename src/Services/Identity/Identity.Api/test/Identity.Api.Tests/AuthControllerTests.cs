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
using Xunit;

namespace Identity.Api.Tests;

public sealed class AuthControllerTests : IDisposable
{
    private readonly AuthDbContext _db;
    private readonly AuthController _controller;

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

        _controller = new AuthController(_db, tokens);
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
