namespace Backend.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record RegisterCustomerRequest(
    string Email,
    string Password,
    string FullName,
    string? Phone,
    string? Address,
    string? IdNumber,
    DateOnly? DateOfBirth,
    string? ConfirmPassword = null);

public record AuthResponse(
    string Token,
    int UserId,
    string Email,
    string FullName,
    string Role,
    DateTime ExpiresAt);

public record RegisterPendingResponse(
    string Email,
    string Message,
    bool RequiresVerification);

public record VerifyEmailRequest(string Email, string Otp);

public record ResendVerificationRequest(string Email);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Otp, string NewPassword, string? ConfirmPassword);

public record GoogleLoginRequest(string IdToken);

public record LoginOptionsResponse(bool GoogleEnabled, string? GoogleClientId);

public record UserProfileResponse(
    int UserId,
    string Email,
    string FullName,
    string? Phone,
    string Role);

public record ChangePasswordRequest(string? OldPassword, string? NewPassword);

public record MessageResponse(string Message);
