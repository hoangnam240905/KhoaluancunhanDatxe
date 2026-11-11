using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Drivers;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Driver eligibility (active, not offline, verified, licence valid through the rental end and of a class
/// that covers the vehicle type — plan1.md Phần D) and date-range availability.
/// </summary>
public class DriverEligibilityService(CarRentalDbContext db, ScheduleConflictService schedule)
{
    public const string Eligible = "Đủ điều kiện";
    public const string Available = "Có thể chọn";
    public const string UnavailableStatus = "Không khả dụng";
    public const string Conflict = "Tài xế đang có chuyến trong khoảng thời gian này.";
    public const string LicenseExpired = "Giấy phép lái xe đã hết hạn.";
    public const string Unavailable = "Tài xế không còn khả dụng. Vui lòng chọn tài xế khác.";
    public const string NotEligible = "Tài xế không đáp ứng yêu cầu lái xe.";
    public const string SelfDriveDriverNotAllowed = "Đơn tự lái không yêu cầu tài xế.";

    public async Task<IReadOnlyList<CustomerDriverOptionResponse>> ListSelectableAsync(
        int? vehicleId, int? vehicleTypeId, DateTime start, DateTime end)
    {
        var typeId = await ResolveVehicleTypeIdAsync(vehicleId, vehicleTypeId);
        if (typeId is null)
            return [];
        var type = await db.VehicleTypes.AsNoTracking().FirstAsync(t => t.TypeId == typeId.Value);

        var drivers = await db.Drivers
            .AsNoTracking()
            .Include(d => d.User)
            .Where(d => d.IsActive && d.Status != DriverStatuses.Offline)
            .OrderBy(d => d.DriverId)
            .ToListAsync();
        await LicenceBook.LoadAsync(db, drivers);

        var result = new List<CustomerDriverOptionResponse>();
        foreach (var driver in drivers)
        {
            if (RejectReason(driver, end) is not null || DriverQualification.ReasonFor(driver, type, end) is not null)
                continue;

            var conflict = await schedule.HasDriverConflictAsync(driver.DriverId, start, end);
            result.Add(new CustomerDriverOptionResponse(
                driver.DriverId,
                driver.User.FullName,
                driver.User.Phone,
                driver.AverageRating,
                driver.TotalTrips,
                driver.LicenseNumber,
                driver.LicenseExpiry,
                Eligible,
                conflict ? UnavailableStatus : Available,
                driver.Status,
                driver.ExperienceYears,
                CanSelect: !conflict));
        }

        return result;
    }

    public async Task<DriverScheduleResponse?> GetScheduleAsync(int driverId, DateTime start, DateTime end)
    {
        var driver = await db.Drivers.AsNoTracking()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return null;

        var busy = await schedule.ListDriverBusyIntervalsAsync(driverId, start, end);
        return new DriverScheduleResponse(
            driver.DriverId,
            driver.User.FullName,
            busy.Select(b => new DriverBusyPeriodResponse(
                b.StartDate, b.EndDate, "Đang bận", b.BookingId, b.Status)).ToList(),
            driver.AverageRating,
            driver.TotalTrips);
    }

    /// <summary>
    /// Revalidates a customer-selected driver for a WithDriver booking.
    /// Returns a Vietnamese reason, or null when the driver may be stored.
    /// </summary>
    public async Task<string?> ValidateSelectionAsync(
        int driverId, int vehicleTypeId, DateTime start, DateTime end)
    {
        var driver = await db.Drivers.AsNoTracking()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return Unavailable;
        await LicenceBook.LoadAsync(db, driver);

        var type = await db.VehicleTypes.AsNoTracking().FirstOrDefaultAsync(t => t.TypeId == vehicleTypeId);
        if (type is null)
            return NotEligible;

        var reason = RejectReason(driver, end) ?? DriverQualification.ReasonFor(driver, type, end);
        if (reason is not null)
            return reason;

        if (await schedule.HasDriverConflictAsync(driverId, start, end))
            return Conflict;

        return null;
    }

    private static string? RejectReason(Entities.Driver driver, DateTime end)
    {
        if (!driver.IsActive)
            return Unavailable;
        if (driver.Status == DriverStatuses.Offline)
            return NotEligible;
        // Phase 9: expired only when every verified licence ends before the return date.
        var approved = LicenceBook.Of(driver).Where(l => l.Status == PersonLicenseStatuses.Approved).ToList();
        if (approved.Count > 0 && approved.All(l => l.Expiry < DateOnly.FromDateTime(end)))
            return LicenseExpired;
        return null;
    }

    private async Task<int?> ResolveVehicleTypeIdAsync(int? vehicleId, int? vehicleTypeId)
    {
        if (vehicleId is int id)
        {
            var vehicle = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == id);
            if (vehicle is null || vehicle.Status == VehicleStatuses.Inactive)
                return null;
            if (vehicleTypeId is int typeId && vehicle.TypeId != typeId)
                return null;
            return vehicle.TypeId;
        }

        if (vehicleTypeId is int onlyType
            && await db.VehicleTypes.AsNoTracking().AnyAsync(t => t.TypeId == onlyType))
            return onlyType;

        return null;
    }
}
