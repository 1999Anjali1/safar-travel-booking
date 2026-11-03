using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Safar.Application.Auth;

namespace Safar.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")] // slows down password guessing
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Create a customer account.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct) =>
        Ok(await auth.RegisterAsync(request, ct));

    /// <summary>Sign in - returns a 15-minute access token and a 7-day refresh token.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await auth.LoginAsync(request, ct));

    /// <summary>Swap a refresh token for a new token pair (the old refresh token is revoked).</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct) =>
        Ok(await auth.RefreshAsync(request, ct));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [DisableRateLimiting]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct) => Ok(await auth.GetCurrentAsync(ct));
}
