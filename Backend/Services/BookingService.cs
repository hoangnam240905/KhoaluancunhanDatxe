using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class BookingService(CarRentalDbContext db, PricingService pricing, IRealtimePublisher? realtime = null)
{
    public async Task<BookingResponse?> CreateBookingAsync(
        int customerId, CreateBookingRequest request, bool fromRecommendation = false)
    {
        if (BookingDateRules.ValidateNewRental(request.StartDate, request.EndDate) is not null)
            return null;

        if (!RentalModes.TryResolve(request.RentalMode, out var rentalMode))
            return null;

        var vehicleType = await db.VehicleTypes
            .FirstOrDefaultAsync(vt => vt.TypeId == request.VehicleTypeId && vt.IsActive);
        if (vehicleType is null) return null;

        var quote = pricing.CalculateQuote(
            vehicleType,
            rentalMode,
            request.StartDate,
            request.EndDate,
            request.EstimatedDistance);

        int? intentVehicleId = null;
        if (request.VehicleId is int vehicleId and > 0)
        {
            var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
            if (vehicle is null) return null;
            if (vehicle.TypeId != request.VehicleTypeId) return null;
            if (vehicle.Status == VehicleStatuses.Inactive) return null;
            intentVehicleId = vehicle.VehicleId;
        }

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
            TotalAmount = quote.TotalAmount,
            QuotedPricePerDay = quote.QuotedPricePerDay,
            QuotedPricePerKm = quote.QuotedPricePerKm,
            QuotedDays = quote.QuotedDays,
            QuotedDriverFeePerDay = quote.QuotedDriverFeePerDay,
            QuotedSelfDriveIncludedKmPerDay = quote.QuotedSelfDriveIncludedKmPerDay,
            QuotedSelfDriveExtraKmPrice = quote.QuotedSelfDriveExtraKmPrice,
            QuotedDepositAmount = quote.DepositAmount,
            Status = BookingStatuses.Pending,
            RentalMode = rentalMode,
            AssignedVehicleId = intentVehicleId,
            SourceRecommended = fromRecommendation,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        await AddStatusHistoryAsync(booking.BookingId, null, BookingStatuses.Pending, customerId, "Khach tao don dat xe");
        await RealtimeNotify.BookingStatusChanged(
            realtime, customerId, booking.BookingId, booking.Status, booking.AssignedVehicleId);
        return await GetBookingByIdAsync(booking.BookingId);
    }

    public async Task<(BookingQuoteResponse? Quote, string? Error)> GetQuoteAsync(
        int vehicleTypeId,
        DateTime startDate,
        DateTime endDate,
        decimal? estimatedDistance,
        string? rentalMode)
    {
        if (vehicleTypeId <= 0)
            return (null, "Loại xe không hợp lệ.");

        var dateError = BookingDateRules.ValidateNewRental(startDate, endDate);
        if (dateError is not null)
            return (null, dateError);

        if (estimatedDistance is < 0)
            return (null, "Km dự kiến không được âm.");

        if (!RentalModes.TryResolve(rentalMode, out var resolvedMode))
            return (null, "Hình thức thuê không hợp lệ.");

        var vehicleType = await db.VehicleTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(vt => vt.TypeId == vehicleTypeId && vt.IsActive);
        if (vehicleType is null)
            return (null, "Loại xe không hợp lệ.");

        var quote = pricing.CalculateQuote(
            vehicleType,
            resolvedMode,
            startDate,
            endDate,
            estimatedDistance);

        return (ToQuoteResponse(vehicleType, resolvedMode, startDate, endDate, quote), null);
    }

    public async Task<List<BookingResponse>> GetBookingsAsync(int? customerId = null, string? status = null)
    {
        var query = db.Bookings
            .Include(b => b.Customer).ThenInclude(c => c.User)
            .Include(b => b.VehicleType)
            .Include(b => b.AssignedVehicle)
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Driver).ThenInclude(d => d.User)
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Vehicle)
            .Include(b => b.Fees)
            .Include(b => b.Inspections)
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
            .Include(b => b.AssignedVehicle)
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Driver).ThenInclude(d => d.User)
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Vehicle)
            .Include(b => b.Fees)
            .Include(b => b.Inspections)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        return booking is null ? null : MapToResponse(booking);
    }

    public Task<(BookingResponse? Data, string? Error, int StatusCode)> UpdateStatusAsync(
        int bookingId, string newStatus, int changedBy, string? note)
        => SqliteWriteLock.ExecuteAsync(db, () =>
            ChangeStatusAsync(bookingId, newStatus, changedBy, note, patch: true));

    public Task<(BookingResponse? Data, string? Error, int StatusCode)> ConfirmPendingAsync(
        int bookingId, int dispatcherId, string? note)
        => SqliteWriteLock.ExecuteAsync(db, () => ChangeStatusAsync(
            bookingId, BookingStatuses.Confirmed, dispatcherId, note, patch: false));

    private async Task<(BookingResponse? Data, string? Error, int StatusCode)> ChangeStatusAsync(
        int bookingId, string newStatus, int changedBy, string? note, bool patch)
    {
        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is null)
            return (null, "Không tìm thấy đơn.", StatusCodes.Status404NotFound);

        var allowed = patch
            ? BookingStateTransitionRules.CanPatch(booking.Status, newStatus)
            : BookingStateTransitionRules.CanConfirm(booking.Status)
              && newStatus == BookingStatuses.Confirmed;

        if (!allowed)
            return (null, BookingStateTransitionRules.InvalidTransition, StatusCodes.Status400BadRequest);

        var oldStatus = booking.Status;
        var heldVehicleId = booking.AssignedVehicleId;
        booking.Status = newStatus;
        booking.UpdatedAt = DateTime.UtcNow;
        if (newStatus == BookingStatuses.Cancelled)
            await ReleaseHeldVehicleAsync(booking);

        await AddStatusHistoryAsync(bookingId, oldStatus, newStatus, changedBy, note);
        Contract? voided = null;
        if (newStatus == BookingStatuses.Cancelled)
        {
            voided = await db.Contracts.FirstOrDefaultAsync(c =>
                c.BookingId == bookingId && c.Status != ContractStatuses.Voided);
            if (voided is not null)
                voided.Status = ContractStatuses.Voided;
        }

        await db.SaveChangesAsync();

        var trip = await db.TripAssignments.AsNoTracking()
            .FirstOrDefaultAsync(t => t.BookingId == bookingId);
        await RealtimeNotify.BookingStatusChanged(
            realtime,
            booking.CustomerId,
            booking.BookingId,
            booking.Status,
            booking.AssignedVehicleId ?? heldVehicleId,
            trip?.DriverId);
        if (newStatus == BookingStatuses.Cancelled && heldVehicleId is int releasedId)
        {
            var released = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == releasedId);
            if (released is not null)
                await RealtimeNotify.VehicleStatusChanged(
                    realtime, released.VehicleId, released.Status, booking.BookingId, booking.CustomerId, trip?.DriverId);
        }
        if (voided is not null)
            await RealtimeNotify.ContractStatusChanged(
                realtime, booking.CustomerId, booking.BookingId, voided.ContractId, voided.Status);

        return (await GetBookingByIdAsync(bookingId), null, StatusCodes.Status200OK);
    }

    public async Task<(ReviewResponse? Data, string? Error)> CreateReviewAsync(
        int bookingId, int customerId, CreateReviewRequest request)
    {
        if (request.Rating is < 1 or > 5)
            return (null, "Không thể đánh giá đơn này.");

        var commentError = ReviewRules.ValidateComment(request.Rating, request.Comment, out var comment);
        if (commentError is not null)
            return (null, commentError);

        var booking = await db.Bookings
            .Include(b => b.TripAssignment)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customerId);

        if (booking is null || booking.Status != BookingStatuses.Completed || booking.TripAssignment is null)
            return (null, "Không thể đánh giá đơn này.");

        if (await db.Reviews.AnyAsync(r => r.BookingId == bookingId))
            return (null, "Không thể đánh giá đơn này.");

        var review = new Review
        {
            BookingId = bookingId,
            CustomerId = customerId,
            DriverId = booking.TripAssignment.DriverId,
            Rating = request.Rating,
            Comment = comment,
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

        return (new ReviewResponse(review.ReviewId, review.BookingId, review.Rating, review.Comment, review.CreatedAt), null);
    }

    private async Task ReleaseHeldVehicleAsync(Booking booking)
    {
        var vehicleId = booking.AssignedVehicleId;
        booking.AssignedVehicleId = null;
        if (vehicleId is not int vid)
            return;

        var vehicle = await db.Vehicles.FindAsync(vid);
        if (vehicle is null || vehicle.Status != VehicleStatuses.Rented)
            return;

        var stillRented = await db.Bookings.AnyAsync(b =>
            b.BookingId != booking.BookingId
            && (b.Status == BookingStatuses.Assigned || b.Status == BookingStatuses.InProgress)
            && (b.AssignedVehicleId == vid
                || (b.TripAssignment != null && b.TripAssignment.VehicleId == vid)));
        if (!stillRented)
            vehicle.Status = VehicleStatuses.Available;
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
        var rentalMode = RentalModes.TryResolve(b.RentalMode, out var resolved)
            ? resolved
            : RentalModes.WithDriver;

        TripAssignmentResponse? assignment = null;
        AssignedVehicleResponse? assignedVehicle = null;

        if (rentalMode == RentalModes.WithDriver
            && b.TripAssignment?.Driver?.User is not null
            && b.TripAssignment.Vehicle is not null)
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

        if (b.AssignedVehicle is not null)
        {
            assignedVehicle = new AssignedVehicleResponse(
                b.AssignedVehicle.VehicleId,
                b.AssignedVehicle.LicensePlate,
                b.AssignedVehicle.Brand,
                b.AssignedVehicle.Model,
                b.AssignedVehicle.Status);
        }

        var fees = (b.Fees ?? [])
            .OrderBy(f => f.FeeId)
            .Select(f => new BookingFeeResponse(f.FeeId, f.FeeType, f.Description, f.Amount, f.CreatedAt))
            .ToList();
        var totalFees = fees
            .Where(f => !BookingFeeTypes.IsIncludedInBase(f.FeeType))
            .Sum(f => f.Amount);
        decimal? finalBaseAmount = b.FinalAmount is null ? null : b.FinalAmount.Value - totalFees;
        var inspections = (b.Inspections ?? [])
            .OrderBy(i => i.InspectionId)
            .Select(VehicleInspectionService.ToResponse)
            .ToList();

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
            assignment,
            rentalMode,
            assignedVehicle,
            b.QuotedPricePerDay,
            b.QuotedPricePerKm,
            b.QuotedDays,
            b.QuotedDriverFeePerDay,
            b.QuotedSelfDriveIncludedKmPerDay,
            b.QuotedSelfDriveExtraKmPrice,
            b.QuotedDepositAmount,
            b.FinalAmount,
            fees,
            finalBaseAmount,
            totalFees,
            inspections);
    }

    private static BookingQuoteResponse ToQuoteResponse(
        VehicleType vehicleType,
        string rentalMode,
        DateTime startDate,
        DateTime endDate,
        PricingQuote quote)
        => new(
            vehicleType.TypeId,
            vehicleType.TypeName,
            rentalMode,
            startDate,
            endDate,
            quote.QuotedPricePerDay,
            quote.QuotedPricePerKm,
            quote.QuotedDays,
            quote.EstimatedDistance,
            quote.RentalAmount,
            quote.DistanceAmount,
            quote.TotalAmount,
            quote.DriverAmount,
            quote.IncludedKm,
            quote.ExtraKm,
            quote.ExtraKmPrice,
            quote.DepositAmount);
}
