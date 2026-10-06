using Backend.Data;
using Backend.DTOs.Customers;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>Customer self-service profile (JWT CustomerId only). Uses existing Customer columns.</summary>
public class CustomerProfileService(CarRentalDbContext db)
{
    private static readonly HashSet<string> IncomeValues =
        new(StringComparer.OrdinalIgnoreCase) { "low", "med", "high", "medium", "mid", "middle" };

    private static readonly HashSet<string> CityValues =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "large", "med", "small", "urban", "big", "metro", "suburban", "medium", "mid", "rural", "countryside"
        };

    private static readonly HashSet<string> PurposeValues =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "city", "trips", "work", "family", "commute", "commuting", "trip", "travel", "vacation", "business"
        };

    public async Task<(CustomerProfileResponse? Data, string? Error, int StatusCode)> GetOwnAsync(int customerId)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer is null)
            return (null, "Không tìm thấy hồ sơ khách hàng.", StatusCodes.Status404NotFound);

        return (Map(customer), null, StatusCodes.Status200OK);
    }

    public async Task<(CustomerProfileResponse? Data, string? Error, int StatusCode)> UpdateOwnAsync(
        int customerId,
        UpdateCustomerProfileRequest request)
    {
        var validation = Validate(request);
        if (validation is not null)
            return (null, validation, StatusCodes.Status400BadRequest);

        var customer = await db.Customers
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer is null)
            return (null, "Không tìm thấy hồ sơ khách hàng.", StatusCodes.Status404NotFound);

        customer.DateOfBirth = request.DateOfBirth;
        customer.NumberOfChildren = request.NumberOfChildren;
        customer.FamilySize = request.FamilySize;
        customer.IncomeRange = NormalizeOptional(request.IncomeRange);
        customer.CityType = NormalizeOptional(request.CityType);
        customer.CommuteKmPerDay = request.CommuteKmPerDay;
        customer.AnnualMileage = request.AnnualMileage;
        customer.RentalPurpose = NormalizeOptional(request.RentalPurpose);
        customer.BudgetMin = request.BudgetMin;
        customer.BudgetMax = request.BudgetMax;
        customer.PreferredVehicleType = NormalizeOptional(request.PreferredVehicleType);
        customer.PreferredFuelType = NormalizeOptional(request.PreferredFuelType);
        customer.PreferredTransmission = NormalizeOptional(request.PreferredTransmission);
        customer.PreferredSeats = request.PreferredSeats;
        customer.PreferencesText = NormalizeOptional(request.PreferencesText);

        if (request.Address is not null)
            customer.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();

        await db.SaveChangesAsync();

        return (Map(customer), null, StatusCodes.Status200OK);
    }

    private static CustomerProfileResponse Map(Customer customer) => new(
        CustomerId: customer.CustomerId,
        Email: customer.User.Email,
        FullName: customer.User.FullName,
        Phone: customer.User.Phone,
        Address: customer.Address,
        DateOfBirth: customer.DateOfBirth,
        NumberOfChildren: customer.NumberOfChildren,
        FamilySize: customer.FamilySize,
        IncomeRange: customer.IncomeRange,
        CityType: customer.CityType,
        CommuteKmPerDay: customer.CommuteKmPerDay,
        AnnualMileage: customer.AnnualMileage,
        RentalPurpose: customer.RentalPurpose,
        BudgetMin: customer.BudgetMin,
        BudgetMax: customer.BudgetMax,
        PreferredVehicleType: customer.PreferredVehicleType,
        PreferredFuelType: customer.PreferredFuelType,
        PreferredTransmission: customer.PreferredTransmission,
        PreferredSeats: customer.PreferredSeats,
        PreferencesText: customer.PreferencesText);

    internal static string? Validate(UpdateCustomerProfileRequest request)
    {
        if (request.DateOfBirth is DateOnly dob)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (dob > today)
                return "Ngày sinh không được ở tương lai.";
            var age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;
            if (age is < 16 or > 100)
                return "Ngày sinh phải tương ứng độ tuổi từ 16 đến 100.";
        }

        if (request.NumberOfChildren is < 0)
            return "Số con không được âm.";
        if (request.NumberOfChildren is > 20)
            return "Số con không hợp lệ.";

        if (request.FamilySize is < 1)
            return "Quy mô gia đình phải từ 1 trở lên.";
        if (request.FamilySize is > 30)
            return "Quy mô gia đình không hợp lệ.";

        if (request.NumberOfChildren is int kids && request.FamilySize is int family && kids >= family)
            return "Quy mô gia đình phải lớn hơn số con.";

        if (!IsAllowedOrEmpty(request.IncomeRange, IncomeValues))
            return "Mức thu nhập không hợp lệ (low / med / high).";

        if (!IsAllowedOrEmpty(request.CityType, CityValues))
            return "Loại khu vực không hợp lệ (large / med / small).";

        if (!IsAllowedOrEmpty(request.RentalPurpose, PurposeValues))
            return "Mục đích thuê xe không hợp lệ.";

        if (request.CommuteKmPerDay is < 0)
            return "Quãng đường đi mỗi ngày không được âm.";
        if (request.CommuteKmPerDay is > 500)
            return "Quãng đường đi mỗi ngày không hợp lệ.";

        if (request.AnnualMileage is < 0)
            return "Số km mỗi năm không được âm.";
        if (request.AnnualMileage is > 200_000)
            return "Số km mỗi năm không hợp lệ.";

        if (request.BudgetMin is < 0 || request.BudgetMax is < 0)
            return "Ngân sách thuê xe không được âm.";
        if (request.BudgetMin is double min && request.BudgetMax is double max && min > max)
            return "Ngân sách tối thiểu không được lớn hơn ngân sách tối đa.";

        if (request.PreferredSeats is int seats && seats is < 1 or > 50)
            return "Số chỗ mong muốn không hợp lệ.";

        if (request.PreferencesText is { Length: > 1000 })
            return "Ghi chú sở thích tối đa 1000 ký tự.";

        return null;
    }

    private static bool IsAllowedOrEmpty(string? value, HashSet<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;
        return allowed.Contains(value.Trim());
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
