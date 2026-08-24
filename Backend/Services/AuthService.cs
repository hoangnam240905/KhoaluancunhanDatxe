using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class AuthService(CarRentalDbContext db, JwtTokenService jwtTokenService)
{
    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return null;

        var (token, expires) = jwtTokenService.CreateToken(
            user.UserId, user.Email, user.FullName, user.Role.RoleName);

        return new AuthResponse(token, user.UserId, user.Email, user.FullName, user.Role.RoleName, expires);
    }

    public async Task<AuthResponse?> RegisterCustomerAsync(RegisterCustomerRequest request)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email))
            return null;

        var customerRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == RoleNames.Customer);
        if (customerRole is null)
            return null;

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName,
            Phone = request.Phone,
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

        return new AuthResponse(token, user.UserId, user.Email, user.FullName, RoleNames.Customer, expires);
    }

    public async Task<UserProfileResponse?> GetProfileAsync(int userId)
    {
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null) return null;

        return new UserProfileResponse(user.UserId, user.Email, user.FullName, user.Phone, user.Role.RoleName);
    }
}
