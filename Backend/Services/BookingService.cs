using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class BookingService(CarRentalDbContext db)
{
    public async Task<BookingResponse?> CreateBookingAsync(int customerId, CreateBookingRequest request)
    {
        if (request.EndDate <= request.StartDate)
            return null;

        var vehicleType = await db.VehicleTypes
            .FirstOrDefaultAsync(vt => vt.TypeId == request.VehicleTypeId && vt.IsActive);
        if (vehicleType is null) return null;

        var days = Math.Max(1, (int)Math.Ceiling((request.EndDate - request.StartDate).TotalDays));
        var distanceCost = (request.EstimatedDistance ?? 0) * vehicleType.PricePerKm;
        var totalAmount = vehicleType.PricePerDay * days + distanceCost;

        var booking = new Booking
        {
            CustomerId = customerId,
            VehicleTypeId = request.VehicleTypeId,
            PickupAddress = request.PickupAddress,
            DropoffAddress = request.DropoffAddress,
            PickupLat = request.PickupLat,
            PickupLng = request.PickupLng,
            DropoffLat = request.DropoffLat,
            DropoffLng = request.DropoffLng,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            EstimatedDistance = request.EstimatedDistance,
            TotalAmount = totalAmount,
            Status = BookingStatuses.Pending,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        await AddStatusHistoryAsync(booking.BookingId, null, BookingStatuses.Pending, customerId, "Khach tao don dat xe");
        return await GetBookingByIdAsync(booking.BookingId);
    }

    public async Task<List<BookingResponse>> GetBookingsAsync(int? customerId = null, string? status = null)
    {
        var query = db.Bookings
            .Include(b => b.Customer).ThenInclude(c => c.User)
            .Include(b => b.VehicleType)
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Driver).ThenInclude(d => d.User)
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Vehicle)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(b => b.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(b => b.Status == status);

        var bookings = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
        return bookings.Select(MapToResponse).ToList();
    }

    public async Task<BookingResponse?> GetBookingByIdAsync(int id)
    {
        var booking = await db.Bookings
            .Include(b => b.Customer).ThenInclude(c => c.User)
            .Include(b => b.VehicleType)
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Driver).ThenInclude(d => d.User)
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Vehicle)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        return booking is null ? null : MapToResponse(booking);
    }

    public async Task<BookingResponse?> UpdateStatusAsync(int bookingId, string newStatus, int changedBy, string? note)
    {
        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is null) return null;

        var oldStatus = booking.Status;
        booking.Status = newStatus;
        booking.UpdatedAt = DateTime.UtcNow;

        await AddStatusHistoryAsync(bookingId, oldStatus, newStatus, changedBy, note);
        await db.SaveChangesAsync();

        return await GetBookingByIdAsync(bookingId);
    }

    public async Task<ReviewResponse?> CreateReviewAsync(int bookingId, int customerId, CreateReviewRequest request)
    {
        if (request.Rating is < 1 or > 5) return null;

        var booking = await db.Bookings
            .Include(b => b.TripAssignment)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customerId);

        if (booking is null || booking.Status != BookingStatuses.Completed || booking.TripAssignment is null)
            return null;

        if (await db.Reviews.AnyAsync(r => r.BookingId == bookingId))
            return null;

        var review = new Review
        {
            BookingId = bookingId,
            CustomerId = customerId,
            DriverId = booking.TripAssignment.DriverId,
            Rating = request.Rating,
            Comment = request.Comment,
            CreatedAt = DateTime.UtcNow
        };

        db.Reviews.Add(review);

        var driver = await db.Drivers.FindAsync(booking.TripAssignment.DriverId);
        if (driver is not null)
        {
            var reviews = await db.Reviews.Where(r => r.DriverId == driver.DriverId).Select(r => (decimal)r.Rating).ToListAsync();
            reviews.Add(request.Rating);
            driver.AverageRating = Math.Round(reviews.Average(), 2);
        }

        await db.SaveChangesAsync();

        return new ReviewResponse(review.ReviewId, review.BookingId, review.Rating, review.Comment, review.CreatedAt);
    }

    private async Task AddStatusHistoryAsync(int bookingId, string? oldStatus, string newStatus, int? changedBy, string? note)
    {
        db.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = bookingId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedBy = changedBy,
            Note = note,
            ChangedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static BookingResponse MapToResponse(Booking b)
    {
        TripAssignmentResponse? assignment = null;
        if (b.TripAssignment is not null)
        {
            assignment = new TripAssignmentResponse(
                b.TripAssignment.AssignmentId,
                b.TripAssignment.DriverId,
                b.TripAssignment.Driver.User.FullName,
                b.TripAssignment.Driver.User.Phone,
                b.TripAssignment.VehicleId,
                b.TripAssignment.Vehicle.LicensePlate,
                b.TripAssignment.Status,
                b.TripAssignment.AssignedAt);
        }

        return new BookingResponse(
            b.BookingId,
            b.CustomerId,
            b.Customer.User.FullName,
            b.VehicleTypeId,
            b.VehicleType.TypeName,
            b.PickupAddress,
            b.DropoffAddress,
            b.StartDate,
            b.EndDate,
            b.EstimatedDistance,
            b.TotalAmount,
            b.Status,
            b.Notes,
            b.CreatedAt,
            assignment);
    }
}
