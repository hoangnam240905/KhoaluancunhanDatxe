using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Customers;
using Backend.Entities;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class AdminCustomerService(CarRentalDbContext db)
{
    public async Task<AdminCustomerListResponse> GetAsync(string? keyword, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = db.Customers.Include(c => c.User).AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.User.FullName.ToLower().Contains(term)
                || c.User.Email.ToLower().Contains(term)
                || (c.User.Phone != null && c.User.Phone.Contains(term)));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderBy(c => c.CustomerId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        var items = rows.Select(Map).ToList();

        return new AdminCustomerListResponse(items, total, page, pageSize);
    }

    public async Task<AdminCustomerResponse?> GetByIdAsync(int id)
    {
        var customer = await db.Customers.Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CustomerId == id);
        return customer is null ? null : Map(customer);
    }

    public async Task<(AdminCustomerResponse? Data, string? Error, int StatusCode)> CreateAsync(
        CreateAdminCustomerRequest request)
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
            return Fail(error, StatusCodes.Status400BadRequest);

        if (await db.Users.AnyAsync(u => u.Email.ToLower() == email))
            return Fail(CustomerRegistrationRules.EmailInUse, StatusCodes.Status400BadRequest);

        if (await db.Users.AnyAsync(u => u.Phone == phone))
            return Fail(CustomerRegistrationRules.PhoneInUse, StatusCodes.Status400BadRequest);

        var customerRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == RoleNames.Customer);
        if (customerRole is null)
            return Fail("Không thể tạo khách hàng.", StatusCodes.Status400BadRequest);

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = fullName,
            Phone = phone,
            RoleId = customerRole.RoleId,
            IsActive = true,
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var customer = new Customer
        {
            CustomerId = user.UserId,
            Address = TrimOrNull(request.Address),
            IdNumber = TrimOrNull(request.IdNumber),
            DateOfBirth = request.DateOfBirth
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var loaded = await db.Customers.Include(c => c.User).FirstAsync(c => c.CustomerId == customer.CustomerId);
        return (Map(loaded), null, StatusCodes.Status201Created);
    }

    public async Task<(AdminCustomerResponse? Data, string? Error, int StatusCode)> UpdateAsync(
        int id, UpdateAdminCustomerRequest request)
    {
        var customer = await db.Customers.Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer is null)
            return Fail("Không tìm thấy khách hàng.", StatusCodes.Status404NotFound);

        var error = CustomerRegistrationRules.ValidateCustomerProfile(
            request.FullName,
            request.Email,
            request.Phone,
            out var fullName,
            out var email,
            out var phone);
        if (error is not null)
            return Fail(error, StatusCodes.Status400BadRequest);

        if (await db.Users.AnyAsync(u => u.UserId != customer.CustomerId && u.Email.ToLower() == email))
            return Fail(CustomerRegistrationRules.EmailInUse, StatusCodes.Status400BadRequest);

        if (await db.Users.AnyAsync(u => u.UserId != customer.CustomerId && u.Phone == phone))
            return Fail(CustomerRegistrationRules.PhoneInUse, StatusCodes.Status400BadRequest);

        customer.User.FullName = fullName;
        customer.User.Email = email;
        customer.User.Phone = phone;
        customer.User.UpdatedAt = DateTime.UtcNow;
        customer.Address = TrimOrNull(request.Address);
        customer.IdNumber = TrimOrNull(request.IdNumber);
        customer.DateOfBirth = request.DateOfBirth;
        await db.SaveChangesAsync();
        return (Map(customer), null, StatusCodes.Status200OK);
    }

    public async Task<(AdminCustomerResponse? Data, string? Error, int StatusCode)> SetLockedAsync(
        int id, bool isLocked)
    {
        var customer = await db.Customers.Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer is null)
            return Fail("Không tìm thấy khách hàng.", StatusCodes.Status404NotFound);

        customer.User.IsLocked = isLocked;
        customer.User.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (Map(customer), null, StatusCodes.Status200OK);
    }

    public async Task<(bool Ok, string? Error, int StatusCode)> DeactivateAsync(int id)
    {
        var customer = await db.Customers.Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer is null)
            return (false, "Không tìm thấy khách hàng.", StatusCodes.Status404NotFound);

        customer.User.IsActive = false;
        customer.User.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, null, StatusCodes.Status200OK);
    }

    private static (AdminCustomerResponse? Data, string? Error, int StatusCode) Fail(string error, int status)
        => (null, error, status);

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static AdminCustomerResponse Map(Customer customer) => new(
        customer.CustomerId,
        customer.User.FullName,
        customer.User.Email,
        customer.User.Phone,
        customer.User.IsLocked,
        customer.User.CreatedAt,
        customer.User.IsActive,
        customer.Address,
        customer.IdNumber,
        customer.DateOfBirth);
}
