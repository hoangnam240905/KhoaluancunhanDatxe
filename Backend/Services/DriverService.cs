using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.DTOs.Drivers;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class DriverService(CarRentalDbContext db, BookingService bookingService)
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

    public async Task<DriverResponse?> UpdateStatusAsync(int driverId, string status)
    {
        if (status is not (DriverStatuses.Available or DriverStatuses.Busy or DriverStatuses.Offline))
            return null;

        var driver = await db.Drivers.Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null) return null;

        driver.Status = status;
        await db.SaveChangesAsync();

        return new DriverResponse(
            driver.DriverId, driver.User.FullName, driver.User.Email, driver.User.Phone,
            driver.LicenseNumber, driver.LicenseExpiry, driver.Status, driver.AverageRating, driver.TotalTrips);
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

    public async Task<bool> CompleteTripAsync(int driverId, int assignmentId)
    {
        var assignment = await db.TripAssignments
            .Include(t => t.Booking)
            .Include(t => t.Driver)
            .Include(t => t.Vehicle)
            .FirstOrDefaultAsync(t => t.AssignmentId == assignmentId && t.DriverId == driverId);

        if (assignment is null || assignment.Status != TripAssignmentStatuses.InProgress)
            return false;

        assignment.Status = TripAssignmentStatuses.Completed;
        assignment.CompletedAt = DateTime.UtcNow;
        assignment.Booking.Status = BookingStatuses.Completed;
        assignment.Booking.UpdatedAt = DateTime.UtcNow;
        assignment.Driver.Status = DriverStatuses.Available;
        assignment.Driver.TotalTrips += 1;
        assignment.Vehicle.Status = VehicleStatuses.Available;

        await db.SaveChangesAsync();
        return true;
    }
}
