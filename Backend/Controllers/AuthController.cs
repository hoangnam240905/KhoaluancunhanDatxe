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
        var result = await authService.LoginAsync(request);
        return result is null ? Unauthorized(new { message = "Email hoặc mật khẩu không đúng." }) : Ok(result);
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterCustomerRequest request)
    {
        var result = await authService.RegisterCustomerAsync(request);
        return result is null ? BadRequest(new { message = "Email đã tồn tại hoặc dữ liệu không hợp lệ." }) : Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileResponse>> GetProfile()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var profile = await authService.GetProfileAsync(userId);
        return profile is null ? NotFound() : Ok(profile);
    }
}
