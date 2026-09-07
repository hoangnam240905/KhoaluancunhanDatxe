using System.Security.Claims;
using Backend.DTOs.Auth;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var (result, error, status) = await authService.LoginAsync(request);
        if (result is not null)
            return Ok(result);

        if (status == StatusCodes.Status403Forbidden)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = error });

        return Unauthorized(new { message = error ?? "Email hoặc mật khẩu không đúng." });
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterCustomerRequest request)
    {
        var (result, error) = await authService.RegisterCustomerAsync(request);
        return result is null
            ? BadRequest(new { message = error ?? "Đăng ký không thành công." })
            : Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileResponse>> GetProfile()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var profile = await authService.GetProfileAsync(userId);
        return profile is null ? NotFound() : Ok(profile);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (ok, error) = await authService.ChangePasswordAsync(userId, request);
        return ok
            ? Ok(new { message = "Đã đổi mật khẩu." })
            : BadRequest(new { message = error ?? "Không thể đổi mật khẩu." });
    }
}
