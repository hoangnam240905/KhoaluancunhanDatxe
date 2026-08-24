using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class DispatchService(CarRentalDbContext db, BookingService bookingService)
{
    public async Task<BookingResponse?> AssignTripAsync(int bookingId, AssignTripRequest request, int dispatcherId)
    {
        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is null || booking.Status is not (BookingStatuses.Pending or BookingStatuses.Confirmed))
            return null;

        if (await db.TripAssignments.AnyAsync(t => t.BookingId == bookingId))
            return null;

        var driver = await db.Drivers
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DriverId == request.DriverId && d.Status == DriverStatuses.Available);
        if (driver is null) return null;

        var vehicle = await db.Vehicles
            .FirstOrDefaultAsync(v => v.VehicleId == request.VehicleId && v.Status == VehicleStatuses.Available);
        if (vehicle is null) return null;

        db.TripAssignments.Add(new TripAssignment
        {
            BookingId = bookingId,
            DriverId = request.DriverId,
            VehicleId = request.VehicleId,
            AssignedBy = dispatcherId,
            AssignedAt = DateTime.UtcNow,
            Status = TripAssignmentStatuses.Assigned
        });

        var oldStatus = booking.Status;
        driver.Status = DriverStatuses.Busy;
        vehicle.Status = VehicleStatuses.Rented;
        booking.Status = BookingStatuses.Assigned;
        booking.UpdatedAt = DateTime.UtcNow;

        db.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = bookingId,
            OldStatus = oldStatus,
            NewStatus = BookingStatuses.Assigned,
            ChangedBy = dispatcherId,
            Note = $"Phan cong tai xe {driver.User.FullName}",
            ChangedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        return await bookingService.GetBookingByIdAsync(bookingId);
    }

    public async Task<BookingResponse?> ConfirmBookingAsync(int bookingId, int dispatcherId)
    {
        return await bookingService.UpdateStatusAsync(bookingId, BookingStatuses.Confirmed, dispatcherId, "Dieu phoi xac nhan don");
    }
}
