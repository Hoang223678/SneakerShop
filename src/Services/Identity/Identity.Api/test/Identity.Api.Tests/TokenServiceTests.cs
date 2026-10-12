using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Identity.Api.Domain;
using Identity.Api.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SneakerShop.BuildingBlocks;
using Xunit;

namespace Identity.Api.Tests;

public sealed class TokenServiceTests
{
    [Fact]
    public void Create_IncludesExpectedSubjectAndRoleClaims()
    {
        var key = "this-is-a-test-secret-key-1234567890";
        var service = new TokenService(Options.Create(new JwtOptions
        {
            Key = key,
            Issuer = "SneakerShop",
            Audience = "SneakerShop-Clients",
            ExpiresMinutes = 45
        }));

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "token-user@example.com",
            FullName = "Token User",
            Role = Roles.Admin
        };

        var (token, expires) = service.Create(user);
        var principal = new JwtSecurityTokenHandler().ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateIssuer = true,
                ValidIssuer = "SneakerShop",
                ValidateAudience = true,
                ValidAudience = "SneakerShop-Clients",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            },
            out _);

        Assert.Equal(user.Id.ToString(), principal.FindFirst("sub")?.Value);
        Assert.Equal(user.Email, principal.FindFirst("email")?.Value);
        Assert.Equal(user.FullName, principal.FindFirst("name")?.Value);
        Assert.Equal(user.Role, principal.FindFirst("role")?.Value);
        Assert.True(expires > DateTime.UtcNow.AddMinutes(44));
    }
}
