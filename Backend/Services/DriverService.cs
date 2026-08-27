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
    BookingFeeService fees)
{
    public async Task<List<DriverResponse>> GetDriversAsync(string? status = null)
    {
        var query = db.Drivers.Include(d => d.User).AsQueryable();

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
        return true;
    }

    public async Task<bool> StartTripAsync(int driverId, int assignmentId)
    {
        var assignment = await db.TripAssignments
            .Include(t => t.Booking)
            .FirstOrDefaultAsync(t => t.AssignmentId == assignmentId && t.DriverId == driverId);

        if (assignment is null || assignment.Status is not (TripAssignmentStatuses.Assigned or TripAssignmentStatuses.Accepted))
            return false;

        assignment.Status = TripAssignmentStatuses.InProgress;
        assignment.StartedAt = DateTime.UtcNow;
        assignment.Booking.Status = BookingStatuses.InProgress;
        assignment.Booking.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
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

        var handover = await inspections.GetHandoverAsync(assignment.BookingId);
        var (inspection, inspectError) = await inspections.AddAsync(
            assignment.BookingId,
            VehicleInspectionTypes.Return,
            request?.OdometerKm,
            request?.FuelLevel,
            request?.Condition,
            request?.Notes);
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
        return (true, null);
    }

    private static DriverResponse MapDriver(Driver driver) => new(
        driver.DriverId, driver.User.FullName, driver.User.Email, driver.User.Phone,
        driver.LicenseNumber, driver.LicenseExpiry, driver.Status, driver.AverageRating, driver.TotalTrips);
}
