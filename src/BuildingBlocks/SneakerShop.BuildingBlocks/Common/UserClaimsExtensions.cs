using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SneakerShop.BuildingBlocks;

public static class UserClaimsExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("sub");
        return Guid.TryParse(value, out var id)
            ? id
            : throw new AppException(StatusCodes.Status401Unauthorized, "Invalid access token.");
    }

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);
}
