using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.DTOs.Drivers;
using Backend.Entities;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class DriverService(
    CarRentalDbContext db,
    BookingService bookingService,
    VehicleInspectionService inspections,
    BookingFeeService fees,
    IRealtimePublisher? realtime = null)
{
    public async Task<List<DriverResponse>> GetDriversAsync(string? status = null)
    {
        var query = db.Drivers.Include(d => d.User).Where(d => d.IsActive).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(d => d.Status == status);

        return await query
            .OrderByDescending(d => d.AverageRating)
            .Select(d => new DriverResponse(
                d.DriverId, d.User.FullName, d.User.Email, d.User.Phone,
                d.LicenseNumber, d.LicenseExpiry, d.Status, d.AverageRating, d.TotalTrips))
            .ToListAsync();
    }

    public async Task<DriverResponse?> GetDriverAsync(int driverId)
    {
        var driver = await db.Drivers.Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DriverId == driverId);
        return driver is null ? null : MapDriver(driver);
    }

    public Task<bool> HasOpenAssignmentAsync(int driverId, int? exceptAssignmentId = null)
        => db.TripAssignments.AnyAsync(t =>
            t.DriverId == driverId
            && (exceptAssignmentId == null || t.AssignmentId != exceptAssignmentId)
            && (t.Status == TripAssignmentStatuses.Assigned
                || t.Status == TripAssignmentStatuses.Accepted
                || t.Status == TripAssignmentStatuses.InProgress));

    public async Task<(DriverResponse? Driver, string? Error)> UpdateStatusAsync(int driverId, string status)
    {
        if (status is not (DriverStatuses.Available or DriverStatuses.Busy or DriverStatuses.Offline))
            return (null, "Trạng thái không hợp lệ.");

        var driver = await db.Drivers.Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null) return (null, "Không tìm thấy tài xế.");

        if (status == DriverStatuses.Available && await HasOpenAssignmentAsync(driverId))
            return (null, "Không thể chuyển Available khi còn chuyến chưa hoàn thành.");

        driver.Status = status;
        await db.SaveChangesAsync();
        await RealtimeNotify.DriverStatusChanged(realtime, driver.DriverId, driver.Status);
        return (MapDriver(driver), null);
    }

    public async Task<List<BookingResponse>> GetDriverTripsAsync(int driverId)
    {
        var bookingIds = await db.TripAssignments
            .Where(t => t.DriverId == driverId)
            .Select(t => t.BookingId)
            .ToListAsync();

        var results = new List<BookingResponse>();
        foreach (var bookingId in bookingIds)
        {
            var booking = await bookingService.GetBookingByIdAsync(bookingId);
            if (booking is not null)
                results.Add(booking);
        }

        return results.OrderByDescending(b => b.CreatedAt).ToList();
    }

    public async Task<bool> AcceptTripAsync(int driverId, int assignmentId)
    {
        var assignment = await db.TripAssignments
            .FirstOrDefaultAsync(t => t.AssignmentId == assignmentId && t.DriverId == driverId);
        if (assignment is null || assignment.Status != TripAssignmentStatuses.Assigned)
            return false;

        assignment.Status = TripAssignmentStatuses.Accepted;
        await db.SaveChangesAsync();
        var booking = await db.Bookings.AsNoTracking().FirstAsync(b => b.BookingId == assignment.BookingId);
        await RealtimeNotify.TripStatusChanged(
            realtime, booking.CustomerId, driverId, assignment.BookingId, assignment.AssignmentId,
            assignment.Status, booking.Status);
        return true;
    }

    public async Task<bool> StartTripAsync(int driverId, int assignmentId)
    {
        var assignment = await db.TripAssignments
            .Include(t => t.Booking)
            .FirstOrDefaultAsync(t => t.AssignmentId == assignmentId && t.DriverId == driverId);

        if (assignment is null || assignment.Status != TripAssignmentStatuses.Accepted)
            return false;
        if (!BookingStateTransitionRules.CanTransition(assignment.Booking.Status, BookingStatuses.InProgress))
            return false;

        assignment.Status = TripAssignmentStatuses.InProgress;
        assignment.StartedAt = DateTime.UtcNow;
        assignment.Booking.Status = BookingStatuses.InProgress;
        assignment.Booking.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await RealtimeNotify.TripStatusChanged(
            realtime,
            assignment.Booking.CustomerId,
            driverId,
            assignment.BookingId,
            assignment.AssignmentId,
            assignment.Status,
            assignment.Booking.Status);
        await RealtimeNotify.BookingStatusChanged(
            realtime,
            assignment.Booking.CustomerId,
            assignment.BookingId,
            assignment.Booking.Status,
            assignment.Booking.AssignedVehicleId,
            driverId);
        return true;
    }

    public async Task<(bool Ok, string? Error)> CompleteTripAsync(
        int driverId, int assignmentId, VehicleConditionRequest? request)
    {
        var assignment = await db.TripAssignments
            .Include(t => t.Booking)
            .Include(t => t.Driver)
            .Include(t => t.Vehicle)
            .FirstOrDefaultAsync(t => t.AssignmentId == assignmentId && t.DriverId == driverId);

        if (assignment is null || assignment.Status != TripAssignmentStatuses.InProgress)
            return (false, null);
        if (!BookingStateTransitionRules.CanTransition(assignment.Booking.Status, BookingStatuses.Completed))
            return (false, BookingStateTransitionRules.InvalidTransition);

        var handover = await inspections.GetHandoverAsync(assignment.BookingId);
        var (inspection, inspectError) = await inspections.AddAsync(
            assignment.BookingId,
            VehicleInspectionTypes.Return,
            request?.OdometerKm,
            request?.FuelLevel,
            request?.Condition,
            request?.Notes,
            request?.ExteriorCondition,
            request?.TechnicalCondition);
        if (inspection is null)
            return (false, inspectError ?? "Không thể ghi nhận trả xe.");

        var actualKm = VehicleInspectionRules.ResolveActualKm(handover?.OdometerKm, request?.OdometerKm);
        await fees.ApplyCompletionAsync(assignment.Booking, actualKm, handover, inspection);

        assignment.Status = TripAssignmentStatuses.Completed;
        assignment.CompletedAt = DateTime.UtcNow;
        assignment.Booking.Status = BookingStatuses.Completed;
        assignment.Booking.UpdatedAt = DateTime.UtcNow;
        assignment.Driver.TotalTrips += 1;
        if (assignment.Vehicle.Status == VehicleStatuses.Rented)
            assignment.Vehicle.Status = VehicleStatuses.Available;

        if (assignment.Driver.Status != DriverStatuses.Offline)
        {
            var stillOpen = await HasOpenAssignmentAsync(driverId, assignment.AssignmentId);
            assignment.Driver.Status = stillOpen ? DriverStatuses.Busy : DriverStatuses.Available;
        }

        await db.SaveChangesAsync();
        await RealtimeNotify.TripStatusChanged(
            realtime,
            assignment.Booking.CustomerId,
            driverId,
            assignment.BookingId,
            assignment.AssignmentId,
            assignment.Status,
            assignment.Booking.Status);
        await RealtimeNotify.BookingStatusChanged(
            realtime,
            assignment.Booking.CustomerId,
            assignment.BookingId,
            assignment.Booking.Status,
            assignment.VehicleId,
            driverId);
        await RealtimeNotify.VehicleStatusChanged(
            realtime, assignment.VehicleId, assignment.Vehicle.Status,
            assignment.BookingId, assignment.Booking.CustomerId, driverId);
        await RealtimeNotify.DriverStatusChanged(realtime, driverId, assignment.Driver.Status);
        return (true, null);
    }

    private static DriverResponse MapDriver(Driver driver) => new(
        driver.DriverId, driver.User.FullName, driver.User.Email, driver.User.Phone,
        driver.LicenseNumber, driver.LicenseExpiry, driver.Status, driver.AverageRating, driver.TotalTrips);

    public async Task<List<AdminDriverResponse>> GetAdminDriversAsync()
    {
        var rows = await db.Drivers.Include(d => d.User)
            .OrderBy(d => d.DriverId)
            .ToListAsync();
        return rows.Select(MapAdmin).ToList();
    }

    public async Task<AdminDriverResponse?> GetAdminDriverAsync(int driverId)
    {
        var driver = await db.Drivers.Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DriverId == driverId);
        return driver is null ? null : MapAdmin(driver);
    }

    public async Task<(AdminDriverResponse? Data, string? Error, int StatusCode)> CreateAdminDriverAsync(
        CreateAdminDriverRequest request)
    {
        var error = CustomerRegistrationRules.ValidateAdminDriver(
            request.FullName, request.Email, request.Phone, request.Password,
            out var fullName, out var email, out var phone);
        if (error is not null)
            return (null, error, StatusCodes.Status400BadRequest);

        if (await db.Users.AnyAsync(u => u.Email.ToLower() == email))
            return (null, CustomerRegistrationRules.EmailInUse, StatusCodes.Status400BadRequest);

        if (await db.Users.AnyAsync(u => u.Phone == phone))
            return (null, CustomerRegistrationRules.PhoneInUse, StatusCodes.Status400BadRequest);

        var driverRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == RoleNames.Driver);
        if (driverRole is null)
            return (null, "Không thể tạo tài xế.", StatusCodes.Status400BadRequest);

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = fullName,
            Phone = phone,
            RoleId = driverRole.RoleId,
            IsActive = true,
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var driver = new Driver
        {
            DriverId = user.UserId,
            LicenseNumber = $"DRV-{user.UserId:D6}",
            LicenseExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(10)),
            Status = DriverStatuses.Available,
            AverageRating = 0,
            TotalTrips = 0,
            IsActive = true
        };
        db.Drivers.Add(driver);
        await db.SaveChangesAsync();

        var loaded = await db.Drivers.Include(d => d.User).FirstAsync(d => d.DriverId == driver.DriverId);
        return (MapAdmin(loaded), null, StatusCodes.Status201Created);
    }

    public async Task<(AdminDriverResponse? Data, string? Error, int StatusCode)> UpdateAdminDriverAsync(
        int driverId, UpdateAdminDriverRequest request)
    {
        var driver = await db.Drivers.Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return (null, "Không tìm thấy tài xế.", StatusCodes.Status404NotFound);

        if (!string.IsNullOrWhiteSpace(request.FullName))
            driver.User.FullName = CustomerRegistrationRules.NormalizeFullName(request.FullName);

        if (request.Phone is not null)
        {
            var phone = CustomerRegistrationRules.NormalizePhone(request.Phone);
            if (!CustomerRegistrationRules.IsValidPhone(phone))
                return (null, CustomerRegistrationRules.InvalidPhone, StatusCodes.Status400BadRequest);
            if (await db.Users.AnyAsync(u => u.UserId != driver.User.UserId && u.Phone == phone))
                return (null, CustomerRegistrationRules.PhoneInUse, StatusCodes.Status400BadRequest);
            driver.User.Phone = phone;
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (request.Status is not (DriverStatuses.Available or DriverStatuses.Busy or DriverStatuses.Offline))
                return (null, "Trạng thái không hợp lệ.", StatusCodes.Status400BadRequest);

            if (await HasOpenAssignmentAsync(driverId)
                && request.Status is DriverStatuses.Available or DriverStatuses.Offline)
            {
                return (null, "Không thể đổi trạng thái khi còn chuyến chưa hoàn thành.",
                    StatusCodes.Status400BadRequest);
            }

            driver.Status = request.Status;
        }

        driver.User.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        if (!string.IsNullOrWhiteSpace(request.Status))
            await RealtimeNotify.DriverStatusChanged(realtime, driver.DriverId, driver.Status);
        return (MapAdmin(driver), null, StatusCodes.Status200OK);
    }

    public async Task<(bool Ok, string? Error, int StatusCode)> SoftDeleteAsync(int driverId)
    {
        var driver = await db.Drivers.Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return (false, "Không tìm thấy tài xế.", StatusCodes.Status404NotFound);

        if (await HasOpenAssignmentAsync(driverId))
            return (false, "Không thể xóa tài xế đang có chuyến chưa hoàn thành.", StatusCodes.Status400BadRequest);

        driver.IsActive = false;
        await db.SaveChangesAsync();
        return (true, null, StatusCodes.Status200OK);
    }

    private static AdminDriverResponse MapAdmin(Driver driver) => new(
        driver.DriverId, driver.User.FullName, driver.User.Email, driver.User.Phone,
        driver.LicenseNumber, driver.LicenseExpiry, driver.Status, driver.AverageRating,
        driver.TotalTrips, driver.IsActive);
}
