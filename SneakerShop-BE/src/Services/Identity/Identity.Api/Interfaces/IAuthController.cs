using Identity.Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Interfaces;

public interface IAuthController
{
    Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct);
    Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct);
    Task<IActionResult> Logout();
    Task<ActionResult<UserDto>> Me(CancellationToken ct);
}
