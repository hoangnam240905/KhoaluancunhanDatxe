using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Backend.Options;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public class BookingService(
    CarRentalDbContext db,
    PricingService pricing,
    IRealtimePublisher? realtime = null,
    DriverEligibilityService? driverEligibility = null,
    IOptions<CancelPolicyOptions>? cancelPolicy = null)
{
    private int CurrentCancelMinHours =>
        cancelPolicy?.Value.MinHoursBeforeStart ?? CancelPolicy.DefaultMinHoursBeforeStart;

    public async Task<BookingResponse?> CreateBookingAsync(
        int customerId, CreateBookingRequest request, bool fromRecommendation = false)
    {
        var (booking, _) = await TryCreateBookingAsync(customerId, request, fromRecommendation);
        return booking;
    }

    public async Task<(BookingResponse? Booking, string? Error)> TryCreateBookingAsync(
        int customerId, CreateBookingRequest request, bool fromRecommendation = false)
    {
        if (BookingDateRules.ValidateNewRental(request.StartDate, request.EndDate) is string dateError)
            return (null, dateError);

        if (!RentalModes.TryResolve(request.RentalMode, out var rentalMode))
            return (null, "Hình thức thuê không hợp lệ.");

        var vehicleType = await db.VehicleTypes
            .FirstOrDefaultAsync(vt => vt.TypeId == request.VehicleTypeId && vt.IsActive);
        if (vehicleType is null)
            return (null, "Loại xe không hợp lệ.");

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
            if (vehicle is null)
                return (null, "Không tìm thấy xe.");
            if (vehicle.TypeId != request.VehicleTypeId)
                return (null, "Xe không thuộc loại xe được đặt.");
            if (vehicle.Status is VehicleStatuses.Inactive or VehicleStatuses.Maintenance)
                return (null, "Xe không khả dụng.");

            // Same interval + 2h buffer rule as Confirm/Search — do not reject solely on Status=Rented.
            var schedule = new ScheduleConflictService(db);
            if (await schedule.GetMaintenanceNewScheduleBlockReasonAsync(vehicleId) is string maintBlock)
                return (null, maintBlock);
            if (await schedule.HasVehicleConflictAsync(vehicleId, request.StartDate, request.EndDate))
            {
                return (null,
                    "Xe không khả dụng trong khoảng thời gian bạn chọn. Vui lòng chọn thời gian khác hoặc xe khác.");
            }

            intentVehicleId = vehicle.VehicleId;
        }

        int? requestedDriverId = null;
        if (rentalMode == RentalModes.SelfDrive)
        {
            if (request.DriverId is int)
                return (null, DriverEligibilityService.SelfDriveDriverNotAllowed);
        }
        else if (request.DriverId is int driverId)
        {
            var drivers = driverEligibility ?? new DriverEligibilityService(db, new ScheduleConflictService(db));
            var driverError = await drivers.ValidateSelectionAsync(
                driverId, request.VehicleTypeId, request.StartDate, request.EndDate);
            if (driverError is not null)
                return (null, driverError);
            requestedDriverId = driverId;
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
            QuotedCancelMinHours = CurrentCancelMinHours,
            Status = BookingStatuses.Pending,
            RentalMode = rentalMode,
            AssignedVehicleId = intentVehicleId,
            RequestedDriverId = requestedDriverId,
            SourceRecommended = fromRecommendation && intentVehicleId.HasValue,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        await AddStatusHistoryAsync(booking.BookingId, null, BookingStatuses.Pending, customerId, "Khach tao don dat xe");
        // Rental history for recommendations: only bookings tied to a concrete vehicle.
        if (intentVehicleId is int bookedVehicle)
            await InteractionLog.TryRecordAsync(db, customerId, bookedVehicle, InteractionLog.Book, booking.BookingId);
        await RealtimeNotify.BookingStatusChanged(
            realtime, customerId, booking.BookingId, booking.Status, booking.AssignedVehicleId);
        return (await GetBookingByIdAsync(booking.BookingId), null);
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
            .Include(b => b.AssignedVehicle).ThenInclude(v => v!.Images)
            .Include(b => b.RequestedDriver).ThenInclude(d => d!.User)
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Driver).ThenInclude(d => d.User)
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Vehicle)
            .Include(b => b.Fees)
            .Include(b => b.Inspections)
            .Include(b => b.Review)
            .Include(b => b.Payments)
            .Include(b => b.Contract)
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
            .Include(b => b.AssignedVehicle).ThenInclude(v => v!.Images)
            .Include(b => b.RequestedDriver).ThenInclude(d => d!.User)
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Driver).ThenInclude(d => d.User)
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Vehicle)
            .Include(b => b.Fees)
            .Include(b => b.Inspections)
            .Include(b => b.Review)
            .Include(b => b.Payments)
            .Include(b => b.Contract)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        return booking is null ? null : MapToResponse(booking);
    }

    public const int CancellationReasonMaxLength = 500;
    public const string CancellationReasonRequired = "Vui lòng nhập lý do hủy đơn.";
    public const string CancellationReasonTooLong = "Lý do hủy đơn quá dài.";
    public const string NotOwner = "Bạn không có quyền hủy đơn thuê này.";

    public Task<(BookingResponse? Data, string? Error, int StatusCode)> CancelByCustomerAsync(
        int bookingId, int customerId, string? reason)
        => SqliteWriteLock.ExecuteAsync(db, () => CancelByCustomerCoreAsync(bookingId, customerId, reason));

    private async Task<(BookingResponse? Data, string? Error, int StatusCode)> CancelByCustomerCoreAsync(
        int bookingId, int customerId, string? reason)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return (null, CancellationReasonRequired, StatusCodes.Status400BadRequest);
        if (trimmed.Length > CancellationReasonMaxLength)
            return (null, CancellationReasonTooLong, StatusCodes.Status400BadRequest);

        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is null)
            return (null, "Không tìm thấy đơn.", StatusCodes.Status404NotFound);
        if (booking.CustomerId != customerId)
            return (null, NotOwner, StatusCodes.Status403Forbidden);
        if (!BookingStateTransitionRules.CanPatch(booking.Status, BookingStatuses.Cancelled))
            return (null, BookingStateTransitionRules.InvalidTransition, StatusCodes.Status400BadRequest);

        var nowVn = VietnamTime.Now;
        if (booking.Status == BookingStatuses.Assigned)
        {
            if (CancelPolicy.HasStarted(booking.StartDate, nowVn))
                return (null, CancelPolicy.AlreadyStarted, StatusCodes.Status400BadRequest);

            var contract = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.BookingId == bookingId);
            var minHours = CancelPolicy.ResolveMinHours(booking, contract, CurrentCancelMinHours);
            if (!CancelPolicy.CanCustomerCancelAssigned(booking.StartDate, nowVn, minHours))
                return (null,
                    CancelPolicy.TooLateForCustomer(minHours, CancelPolicy.CustomerDeadline(booking.StartDate, minHours)),
                    StatusCodes.Status400BadRequest);
        }

        booking.CancellationReason = trimmed;
        return await ChangeStatusAsync(bookingId, BookingStatuses.Cancelled, customerId, trimmed, patch: true, nowVn);
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
        int bookingId, string newStatus, int changedBy, string? note, bool patch, DateTime? nowVn = null)
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

        var cancelling = newStatus == BookingStatuses.Cancelled;
        // An Assigned trip past its start time is effectively running/overdue: dispatcher must use return flow.
        if (cancelling
            && booking.Status == BookingStatuses.Assigned
            && CancelPolicy.HasStarted(booking.StartDate, nowVn ?? VietnamTime.Now))
            return (null, CancelPolicy.AlreadyStarted, StatusCodes.Status400BadRequest);

        var oldStatus = booking.Status;
        TripAssignment? trip = null;
        int? releasedVehicleId = null;
        Driver? releasedDriver = null;
        booking.Status = newStatus;
        booking.UpdatedAt = DateTime.UtcNow;
        if (cancelling)
        {
            trip = await db.TripAssignments.FirstOrDefaultAsync(t => t.BookingId == bookingId);
            // With-driver bookings may hold the car only through TripAssignment.VehicleId.
            releasedVehicleId = booking.AssignedVehicleId ?? trip?.VehicleId;
            if (trip is not null && TripAssignmentStatuses.IsOpen(trip.Status))
            {
                trip.Status = TripAssignmentStatuses.Cancelled;
                releasedDriver = await ReleaseDriverAsync(trip);
            }

            booking.AssignedVehicleId = null;
            // Close the trip first so it no longer counts as "still using" the vehicle.
            if (releasedVehicleId is int vid)
                await VehicleOccupancy.ReleaseIfIdleAsync(db, vid, bookingId);

            if (await HasPaidDepositAsync(bookingId))
                note = string.IsNullOrWhiteSpace(note)
                    ? CancelPolicy.ManualDepositRefundNote
                    : $"{note} — {CancelPolicy.ManualDepositRefundNote}";
        }

        await AddStatusHistoryAsync(bookingId, oldStatus, newStatus, changedBy, note);
        Contract? voided = null;
        if (cancelling)
        {
            voided = await db.Contracts.FirstOrDefaultAsync(c =>
                c.BookingId == bookingId && c.Status != ContractStatuses.Voided);
            if (voided is not null)
                voided.Status = ContractStatuses.Voided;
        }

        await db.SaveChangesAsync();

        if (cancelling && releasedVehicleId is int cancelledVehicle)
            await InteractionLog.TryRecordAsync(db, booking.CustomerId, cancelledVehicle, InteractionLog.Cancel, bookingId);

        trip ??= await db.TripAssignments.AsNoTracking()
            .FirstOrDefaultAsync(t => t.BookingId == bookingId);
        await RealtimeNotify.BookingStatusChanged(
            realtime,
            booking.CustomerId,
            booking.BookingId,
            booking.Status,
            booking.AssignedVehicleId ?? releasedVehicleId,
            trip?.DriverId);
        if (cancelling && releasedVehicleId is int releasedId)
        {
            var released = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == releasedId);
            if (released is not null)
                await RealtimeNotify.VehicleStatusChanged(
                    realtime, released.VehicleId, released.Status, booking.BookingId, booking.CustomerId, trip?.DriverId);
        }
        if (trip is not null && trip.Status == TripAssignmentStatuses.Cancelled && releasedDriver is not null)
        {
            await RealtimeNotify.AssignmentChanged(
                realtime, booking.CustomerId, booking.BookingId, trip.AssignmentId,
                trip.VehicleId, trip.DriverId, booking.Status);
            await RealtimeNotify.DriverStatusChanged(realtime, releasedDriver.DriverId, releasedDriver.Status);
        }
        if (voided is not null)
            await RealtimeNotify.ContractStatusChanged(
                realtime, booking.CustomerId, booking.BookingId, voided.ContractId, voided.Status);

        return (await GetBookingByIdAsync(bookingId), null, StatusCodes.Status200OK);
    }

    /// <summary>Driver goes back to Available unless Offline or still holding another open trip.</summary>
    private async Task<Driver?> ReleaseDriverAsync(TripAssignment trip)
    {
        var driver = await db.Drivers.FindAsync(trip.DriverId);
        if (driver is null)
            return null;
        if (driver.Status != DriverStatuses.Offline)
        {
            var stillOpen = await db.TripAssignments.AnyAsync(t =>
                t.DriverId == trip.DriverId
                && t.AssignmentId != trip.AssignmentId
                && (t.Status == TripAssignmentStatuses.Assigned
                    || t.Status == TripAssignmentStatuses.Accepted
                    || t.Status == TripAssignmentStatuses.InProgress));
            driver.Status = stillOpen ? DriverStatuses.Busy : DriverStatuses.Available;
        }
        return driver;
    }

    private Task<bool> HasPaidDepositAsync(int bookingId)
        => db.Payments.AnyAsync(p =>
            p.BookingId == bookingId
            && p.PaymentType == PaymentTypes.Deposit
            && p.Status == PaymentStatuses.Paid);

    public async Task<(ReviewResponse? Data, string? Error)> CreateReviewAsync(
        int bookingId, int customerId, CreateReviewRequest request)
    {
        if (request.Rating is < 1 or > 5)
            return (null, "Không thể đánh giá đơn này.");

        var commentError = ReviewRules.ValidateComment(request.Rating, request.Comment, out var comment);
        if (commentError is not null)
            return (null, commentError);

        var detailError = ReviewRules.ValidateOptionalScore(request.ComfortRating)
            ?? ReviewRules.ValidateOptionalScore(request.CleanlinessRating)
            ?? ReviewRules.ValidateOptionalScore(request.PerformanceRating)
            ?? ReviewRules.ValidateOptionalScore(request.ValueRating);
        if (detailError is not null)
            return (null, detailError);

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
            ComfortRating = request.ComfortRating,
            CleanlinessRating = request.CleanlinessRating,
            PerformanceRating = request.PerformanceRating,
            ValueRating = request.ValueRating,
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

        return (MapReview(review), null);
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

    private BookingResponse MapToResponse(Booking b)
    {
        var (customerCanCancel, customerCancelDeadline) = CustomerCancelState(b, VietnamTime.Now);
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
                b.AssignedVehicle.Status,
                VehicleService.PrimaryImage(b.AssignedVehicle));
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
        var review = b.Review is null ? null : MapReview(b.Review);

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
            inspections,
            review,
            HasActiveDeposit(b),
            b.CancellationReason,
            b.RequestedDriverId,
            b.RequestedDriver?.User.FullName,
            customerCanCancel,
            customerCancelDeadline);
    }

    /// <summary>
    /// Backend is the only place computing the customer cancel window (web servers may run in another time zone).
    /// Pending/Confirmed keep the existing rule; Assigned uses the snapshotted window.
    /// </summary>
    private (bool CanCancel, DateTime? DeadlineVn) CustomerCancelState(Booking b, DateTime nowVn)
    {
        if (b.Status is BookingStatuses.Pending or BookingStatuses.Confirmed)
            return (true, null);
        if (b.Status != BookingStatuses.Assigned)
            return (false, null);

        var minHours = CancelPolicy.ResolveMinHours(b, b.Contract, CurrentCancelMinHours);
        return (CancelPolicy.CanCustomerCancelAssigned(b.StartDate, nowVn, minHours),
            CancelPolicy.CustomerDeadline(b.StartDate, minHours));
    }

    private static ReviewResponse MapReview(Review review) => new(
        review.ReviewId,
        review.BookingId,
        review.Rating,
        review.Comment,
        review.CreatedAt,
        review.ComfortRating,
        review.CleanlinessRating,
        review.PerformanceRating,
        review.ValueRating,
        review.CustomerId,
        review.DriverId);

    private static bool HasActiveDeposit(Booking b)
        => (b.Payments ?? []).Any(p =>
            p.PaymentType == PaymentTypes.Deposit
            && (p.Status == PaymentStatuses.Pending || p.Status == PaymentStatuses.Paid));

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
