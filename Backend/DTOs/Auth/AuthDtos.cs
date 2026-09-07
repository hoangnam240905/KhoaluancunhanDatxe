namespace Backend.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record RegisterCustomerRequest(
    string Email,
    string Password,
    string FullName,
    string? Phone,
    string? Address,
    string? IdNumber,
    DateOnly? DateOfBirth);

public record AuthResponse(
    string Token,
    int UserId,
    string Email,
    string FullName,
    string Role,
    DateTime ExpiresAt);

public record UserProfileResponse(
    int UserId,
    string Email,
    string FullName,
    string? Phone,
    string Role);

public record ChangePasswordRequest(string? OldPassword, string? NewPassword);
