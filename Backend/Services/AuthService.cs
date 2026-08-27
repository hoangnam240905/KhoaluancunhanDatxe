using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.Entities;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class AuthService(CarRentalDbContext db, JwtTokenService jwtTokenService)
{
    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.IsActive && u.Email.ToLower() == email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return null;

        var (token, expires) = jwtTokenService.CreateToken(
            user.UserId, user.Email, user.FullName, user.Role.RoleName);

        return new AuthResponse(token, user.UserId, user.Email, user.FullName, user.Role.RoleName, expires);
    }

    public async Task<(AuthResponse? Data, string? Error)> RegisterCustomerAsync(RegisterCustomerRequest request)
    {
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

        if (await db.Users.AnyAsync(u => u.Email.ToLower() == email))
            return (null, CustomerRegistrationRules.EmailInUse);

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

        var (token, expires) = jwtTokenService.CreateToken(
            user.UserId, user.Email, user.FullName, RoleNames.Customer);

        return (new AuthResponse(token, user.UserId, user.Email, user.FullName, RoleNames.Customer, expires), null);
    }

    public async Task<UserProfileResponse?> GetProfileAsync(int userId)
    {
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null) return null;

        return new UserProfileResponse(user.UserId, user.Email, user.FullName, user.Phone, user.Role.RoleName);
    }
}
