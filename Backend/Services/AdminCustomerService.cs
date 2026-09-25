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
        var names = await ActorNamesAsync(rows);
        var items = rows.Select(c => Map(c, names)).ToList();

        return new AdminCustomerListResponse(items, total, page, pageSize);
    }

    public async Task<AdminCustomerResponse?> GetByIdAsync(int id)
    {
        var customer = await db.Customers.Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer is null) return null;
        var names = await ActorNamesAsync([customer]);
        return Map(customer, names);
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
            IsEmailVerified = true,
            EmailVerifiedAt = DateTime.UtcNow,
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
        return (Map(loaded, new Dictionary<int, string>()), null, StatusCodes.Status201Created);
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
        var names = await ActorNamesAsync([customer]);
        return (Map(customer, names), null, StatusCodes.Status200OK);
    }

    public async Task<(AdminCustomerResponse? Data, string? Error, int StatusCode)> SetLockedAsync(
        int id, bool isLocked, string? reason = null, int? actorUserId = null)
    {
        var customer = await db.Customers.Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer is null)
            return Fail("Không tìm thấy khách hàng.", StatusCodes.Status404NotFound);

        if (isLocked)
        {
            var reasonError = CustomerAccountStatusRules.ValidateLockReason(reason, out var normalized);
            if (reasonError is not null)
                return Fail(reasonError, StatusCodes.Status400BadRequest);

            customer.User.IsLocked = true;
            customer.User.LockReason = normalized;
            customer.User.LockedAt = DateTime.UtcNow;
            customer.User.LockedByUserId = actorUserId;
        }
        else
        {
            customer.User.IsLocked = false;
            customer.User.LockReason = null;
            customer.User.LockedAt = null;
            customer.User.LockedByUserId = null;
        }

        customer.User.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var names = await ActorNamesAsync([customer]);
        return (Map(customer, names), null, StatusCodes.Status200OK);
    }

    public async Task<(bool Ok, string? Error, int StatusCode)> DeactivateAsync(
        int id, string? reason = null, int? actorUserId = null)
    {
        var reasonError = CustomerAccountStatusRules.ValidateInactiveReason(reason, out var normalized);
        if (reasonError is not null)
            return (false, reasonError, StatusCodes.Status400BadRequest);

        var customer = await db.Customers.Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer is null)
            return (false, "Không tìm thấy khách hàng.", StatusCodes.Status404NotFound);

        customer.User.IsActive = false;
        customer.User.InactiveReason = normalized;
        customer.User.InactivatedAt = DateTime.UtcNow;
        customer.User.InactivatedByUserId = actorUserId;
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

    private async Task<Dictionary<int, string>> ActorNamesAsync(IEnumerable<Customer> customers)
    {
        var ids = customers
            .SelectMany(c => new int?[] { c.User.LockedByUserId, c.User.InactivatedByUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return [];

        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName);
    }

    private static AdminCustomerResponse Map(Customer customer, IReadOnlyDictionary<int, string> names)
    {
        string? ActorName(int? id)
            => id is int value && names.TryGetValue(value, out var name) ? name : null;

        return new(
            customer.CustomerId,
            customer.User.FullName,
            customer.User.Email,
            customer.User.Phone,
            customer.User.IsLocked,
            customer.User.CreatedAt,
            customer.User.IsActive,
            customer.Address,
            customer.IdNumber,
            customer.DateOfBirth,
            customer.User.LockReason,
            customer.User.LockedAt,
            customer.User.LockedByUserId,
            ActorName(customer.User.LockedByUserId),
            customer.User.InactiveReason,
            customer.User.InactivatedAt,
            customer.User.InactivatedByUserId,
            ActorName(customer.User.InactivatedByUserId));
    }
}
