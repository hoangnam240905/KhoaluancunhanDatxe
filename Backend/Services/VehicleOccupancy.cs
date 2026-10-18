using Backend.Constants;
using Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Single place deciding when a Rented vehicle goes back to Available. A vehicle can be held either
/// through Booking.AssignedVehicleId (self-drive / customer-picked) or TripAssignment.VehicleId (with driver).
/// </summary>
public static class VehicleOccupancy
{
    /// <summary>
    /// Sets the vehicle Available when it is Rented and no other Assigned/InProgress booking still uses it.
    /// Maintenance/Inactive vehicles are never touched. Returns true when the status changed.
    /// </summary>
    public static async Task<bool> ReleaseIfIdleAsync(CarRentalDbContext db, int vehicleId, int exceptBookingId)
    {
        var vehicle = await db.Vehicles.FindAsync(vehicleId);
        if (vehicle is null || vehicle.Status != VehicleStatuses.Rented)
            return false;
        if (await IsUsedByOtherActiveBookingAsync(db, vehicleId, exceptBookingId))
            return false;

        vehicle.Status = VehicleStatuses.Available;
        return true;
    }

    public static Task<bool> IsUsedByOtherActiveBookingAsync(CarRentalDbContext db, int vehicleId, int exceptBookingId)
        => db.Bookings.AnyAsync(b =>
            b.BookingId != exceptBookingId
            && (b.Status == BookingStatuses.Assigned || b.Status == BookingStatuses.InProgress)
            && (b.AssignedVehicleId == vehicleId
                || (b.TripAssignment != null
                    && b.TripAssignment.VehicleId == vehicleId
                    && (b.TripAssignment.Status == TripAssignmentStatuses.Assigned
                        || b.TripAssignment.Status == TripAssignmentStatuses.Accepted
                        || b.TripAssignment.Status == TripAssignmentStatuses.InProgress))));
}
