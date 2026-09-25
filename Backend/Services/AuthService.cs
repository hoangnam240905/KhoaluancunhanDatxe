using System.Security.Cryptography;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.Entities;
using Backend.Options;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public class AuthService(
    CarRentalDbContext db,
    JwtTokenService jwtTokenService,
    EmailOtpService otpService,
    IGoogleIdTokenValidator googleIdTokenValidator,
    IOptions<GoogleAuthOptions> googleOptions)
{
    public LoginOptionsResponse GetLoginOptions()
    {
        var google = googleOptions.Value;
        return new LoginOptionsResponse(google.IsConfigured, google.IsConfigured ? google.ClientId : null);
    }

    public async Task<(AuthResponse? Data, string? Error, int StatusCode)> LoginAsync(LoginRequest request)
    {
        var email = EmailOtpService.NormalizeEmail(request.Email);
        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return (null, "Email hoặc mật khẩu không đúng.", StatusCodes.Status401Unauthorized);

        if (!user.IsActive)
            return (null, "Tài khoản đã bị vô hiệu hóa.", StatusCodes.Status403Forbidden);

        if (user.IsLocked)
            return (null, "Tài khoản đã bị khóa.", StatusCodes.Status403Forbidden);

        if (user.Role.RoleName == RoleNames.Customer && !user.IsEmailVerified)
            return (null, CustomerRegistrationRules.EmailNotVerified, StatusCodes.Status403Forbidden);

        return (IssueAuth(user), null, StatusCodes.Status200OK);
    }

    public async Task<(RegisterPendingResponse? Data, string? Error)> RegisterCustomerAsync(RegisterCustomerRequest request)
    {
        if (!string.IsNullOrEmpty(request.ConfirmPassword) && request.ConfirmPassword != request.Password)
            return (null, CustomerRegistrationRules.ConfirmPasswordMismatch);

        var error = CustomerRegistrationRules.ValidateAndNormalize(
            request.FullName,
            request.Email,
            request.Phone,
            request.Password,
            out var fullName,
            out var email,
            out var phone);

        if (error is not null)
            return (null, error);

        var existing = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (existing is not null)
        {
            if (existing.Role.RoleName != RoleNames.Customer || existing.IsEmailVerified)
                return (null, CustomerRegistrationRules.EmailInUse);

            if (await db.Users.AnyAsync(u => u.UserId != existing.UserId && u.Phone == phone))
                return (null, CustomerRegistrationRules.PhoneInUse);

            existing.FullName = fullName;
            existing.Phone = phone;
            existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var (resent, resendError) = await otpService.IssueAsync(email, OtpPurposes.EmailVerification);
            return resent
                ? (new RegisterPendingResponse(email, EmailOtpService.SentVerification, true), null)
                : (null, resendError ?? "Không thể gửi mã OTP.");
        }

        if (await db.Users.AnyAsync(u => u.Phone == phone))
            return (null, CustomerRegistrationRules.PhoneInUse);

        var customerRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == RoleNames.Customer);
        if (customerRole is null)
            return (null, "Đăng ký không thành công.");

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = fullName,
            Phone = phone,
            RoleId = customerRole.RoleId,
            IsActive = true,
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.Customers.Add(new Customer
        {
            CustomerId = user.UserId,
            Address = request.Address,
            IdNumber = request.IdNumber,
            DateOfBirth = request.DateOfBirth
        });
        await db.SaveChangesAsync();

        var (sent, sendError) = await otpService.IssueAsync(email, OtpPurposes.EmailVerification);
        if (!sent)
            return (new RegisterPendingResponse(email, sendError ?? EmailOtpService.SentVerification, true), null);

        return (new RegisterPendingResponse(email, EmailOtpService.SentVerification, true), null);
    }

    public async Task<(AuthResponse? Data, string? Error)> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var email = EmailOtpService.NormalizeEmail(request.Email);
        var (ok, error) = await otpService.VerifyAsync(email, OtpPurposes.EmailVerification, request.Otp);
        if (!ok)
            return (null, error);

        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user is null || user.Role.RoleName != RoleNames.Customer)
            return (null, EmailOtpService.InvalidOtp);

        user.IsEmailVerified = true;
        user.EmailVerifiedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (IssueAuth(user), null);
    }

    public async Task<(RegisterPendingResponse? Data, string? Error)> ResendVerificationAsync(ResendVerificationRequest request)
    {
        var email = EmailOtpService.NormalizeEmail(request.Email);
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user is null || user.Role.RoleName != RoleNames.Customer || user.IsEmailVerified)
            return (null, "Không thể gửi lại mã xác minh.");

        var (ok, error) = await otpService.IssueAsync(email, OtpPurposes.EmailVerification);
        return ok
            ? (new RegisterPendingResponse(email, EmailOtpService.SentVerification, true), null)
            : (null, error ?? "Không thể gửi lại mã xác minh.");
    }

    public async Task<MessageResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var email = EmailOtpService.NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user is not null && user.IsActive && !user.IsLocked)
            await otpService.IssueAsync(email, OtpPurposes.PasswordReset);

        return new MessageResponse(EmailOtpService.ForgotGeneric);
    }

    public async Task<(bool Ok, string? Error)> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (!string.IsNullOrEmpty(request.ConfirmPassword) && request.ConfirmPassword != request.NewPassword)
            return (false, CustomerRegistrationRules.ConfirmPasswordMismatch);

        if (!CustomerRegistrationRules.IsStrongPassword(request.NewPassword))
            return (false, CustomerRegistrationRules.InvalidPassword);

        var email = EmailOtpService.NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user is null)
            return (false, EmailOtpService.InvalidOtp);

        var (ok, error) = await otpService.VerifyAsync(email, OtpPurposes.PasswordReset, request.Otp);
        if (!ok)
            return (false, error);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.IsEmailVerified = true;
        user.EmailVerifiedAt ??= DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(AuthResponse? Data, string? Error, int StatusCode)> GoogleLoginAsync(GoogleLoginRequest request)
    {
        if (!googleOptions.Value.IsConfigured)
            return (null, "Đăng nhập Google chưa được cấu hình.", StatusCodes.Status503ServiceUnavailable);

        var identity = await googleIdTokenValidator.ValidateAsync(request.IdToken ?? string.Empty);
        if (identity is null)
            return (null, "Đăng nhập Google không hợp lệ.", StatusCodes.Status401Unauthorized);

        var email = EmailOtpService.NormalizeEmail(identity.Email);
        if (string.IsNullOrEmpty(email))
            return (null, "Đăng nhập Google không hợp lệ.", StatusCodes.Status401Unauthorized);

        var bySubject = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.GoogleSubject == identity.Subject);
        if (bySubject is not null)
            return CompleteGoogleUser(bySubject);

        var existing = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (existing is not null)
        {
            if (existing.Role.RoleName != RoleNames.Customer)
                return (null, "Email Google này không thể đăng nhập với vai trò khách hàng.", StatusCodes.Status403Forbidden);

            if (!string.IsNullOrEmpty(existing.GoogleSubject) && existing.GoogleSubject != identity.Subject)
                return (null, "Email đã được liên kết với tài khoản Google khác.", StatusCodes.Status403Forbidden);

            existing.GoogleSubject = identity.Subject;
            existing.IsEmailVerified = true;
            existing.EmailVerifiedAt ??= DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return CompleteGoogleUser(existing);
        }

        var customerRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == RoleNames.Customer);
        if (customerRole is null)
            return (null, "Đăng nhập Google không thành công.", StatusCodes.Status500InternalServerError);

        var fullName = string.IsNullOrWhiteSpace(identity.Name)
            ? email.Split('@')[0]
            : CustomerRegistrationRules.NormalizeFullName(identity.Name);

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
            FullName = fullName,
            RoleId = customerRole.RoleId,
            IsActive = true,
            IsEmailVerified = true,
            EmailVerifiedAt = DateTime.UtcNow,
            GoogleSubject = identity.Subject,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.Customers.Add(new Customer { CustomerId = user.UserId });
        await db.SaveChangesAsync();

        user.Role = customerRole;
        return (IssueAuth(user), null, StatusCodes.Status200OK);
    }

    public async Task<UserProfileResponse?> GetProfileAsync(int userId)
    {
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null) return null;

        return new UserProfileResponse(user.UserId, user.Email, user.FullName, user.Phone, user.Role.RoleName);
    }

    public async Task<(bool Ok, string? Error)> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        if (!CustomerRegistrationRules.IsStrongPassword(request.NewPassword))
            return (false, CustomerRegistrationRules.InvalidPassword);

        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null)
            return (false, "Không tìm thấy tài khoản.");

        if (!BCrypt.Net.BCrypt.Verify(request.OldPassword ?? string.Empty, user.PasswordHash))
            return (false, "Mật khẩu cũ không đúng.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, null);
    }

    private (AuthResponse? Data, string? Error, int StatusCode) CompleteGoogleUser(User user)
    {
        if (!user.IsActive)
            return (null, "Tài khoản đã bị vô hiệu hóa.", StatusCodes.Status403Forbidden);
        if (user.IsLocked)
            return (null, "Tài khoản đã bị khóa.", StatusCodes.Status403Forbidden);
        if (user.Role.RoleName != RoleNames.Customer)
            return (null, "Email Google này không thể đăng nhập với vai trò khách hàng.", StatusCodes.Status403Forbidden);

        return (IssueAuth(user), null, StatusCodes.Status200OK);
    }

    private AuthResponse IssueAuth(User user)
    {
        var (token, expires) = jwtTokenService.CreateToken(
            user.UserId, user.Email, user.FullName, user.Role.RoleName);
        return new AuthResponse(token, user.UserId, user.Email, user.FullName, user.Role.RoleName, expires);
    }
}
