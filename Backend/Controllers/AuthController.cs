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
    [HttpGet("login-options")]
    public ActionResult<LoginOptionsResponse> LoginOptions() => Ok(authService.GetLoginOptions());

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
    public async Task<ActionResult<RegisterPendingResponse>> Register(RegisterCustomerRequest request)
    {
        var (result, error) = await authService.RegisterCustomerAsync(request);
        return result is null
            ? BadRequest(new { message = error ?? "Đăng ký không thành công." })
            : Ok(result);
    }

    [HttpPost("verify-email")]
    public async Task<ActionResult<AuthResponse>> VerifyEmail(VerifyEmailRequest request)
    {
        var (result, error) = await authService.VerifyEmailAsync(request);
        return result is null
            ? BadRequest(new { message = error ?? "Không thể xác minh email." })
            : Ok(result);
    }

    [HttpPost("resend-verification-otp")]
    public async Task<ActionResult<RegisterPendingResponse>> ResendVerification(ResendVerificationRequest request)
    {
        var (result, error) = await authService.ResendVerificationAsync(request);
        return result is null
            ? BadRequest(new { message = error ?? "Không thể gửi lại mã xác minh." })
            : Ok(result);
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<MessageResponse>> ForgotPassword(ForgotPasswordRequest request)
    {
        return Ok(await authService.ForgotPasswordAsync(request));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var (ok, error) = await authService.ResetPasswordAsync(request);
        return ok
            ? Ok(new { message = "Đã đặt lại mật khẩu." })
            : BadRequest(new { message = error ?? "Không thể đặt lại mật khẩu." });
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> Google(GoogleLoginRequest request)
    {
        var (result, error, status) = await authService.GoogleLoginAsync(request);
        if (result is not null)
            return Ok(result);

        return StatusCode(status, new { message = error ?? "Đăng nhập Google không thành công." });
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
