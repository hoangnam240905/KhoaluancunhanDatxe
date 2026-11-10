using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.DTOs.Drivers;
using Backend.DTOs.Realtime;
using Backend.Entities;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class DriverService(
    CarRentalDbContext db,
    BookingService bookingService,
    VehicleInspectionService inspections,
    BookingFeeService fees,
    ScheduleConflictService schedule,
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

    public const string PreHandoverMissing =
        "Chưa có biên bản kiểm tra trước giao của điều phối. Bấm \"Nhắc điều phối\" để báo điều phối ghi biên bản.";
    public const string DisputeOpen = "Đang chờ điều phối xử lý sai lệch biên bản trước giao.";
    public const string DisputeNoteRequired = "Vui lòng ghi rõ điểm sai lệch.";
    public const string DisputeNoteTooLong = "Ghi chú sai lệch tối đa 500 ký tự.";
    public const string AlreadyHasPreHandover = "Điều phối đã ghi biên bản trước giao, vui lòng mở chuyến để kiểm tra.";
    public const string TripNotWaitingToStart = "Chuyến này không còn ở bước chờ bắt đầu.";

    /// <summary>
    /// Driver says the dispatcher's pre-handover record does not match the real car: opens a dispute,
    /// tells the dispatchers. The trip does not start and the assignment stays as it is.
    /// </summary>
    public async Task<(InspectionDispute? Dispute, string? Error)> RaiseDisputeAsync(
        int driverId, int assignmentId, string? note)
    {
        var text = note?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return (null, DisputeNoteRequired);
        if (text.Length > 500)
            return (null, DisputeNoteTooLong);

        var assignment = await db.TripAssignments
            .FirstOrDefaultAsync(t => t.AssignmentId == assignmentId && t.DriverId == driverId);
        if (assignment is null || assignment.Status is not (TripAssignmentStatuses.Assigned or TripAssignmentStatuses.Accepted))
            return (null, TripNotWaitingToStart);
        var preHandover = await inspections.GetPreHandoverAsync(assignment.BookingId);
        if (preHandover is null)
            return (null, PreHandoverMissing);
        if (await HasOpenDisputeAsync(assignment.BookingId))
            return (null, DisputeOpen);

        var actor = await ActorLookup.FindAsync(db, driverId);
        var dispute = new InspectionDispute
        {
            BookingId = assignment.BookingId,
            InspectionId = preHandover.InspectionId,
            RaisedByUserId = actor?.UserId,
            RaisedByName = actor?.Name,
            Note = text,
            Status = InspectionDisputeStatuses.Open,
            CreatedAt = DateTime.UtcNow
        };
        db.InspectionDisputes.Add(dispute);
        await db.SaveChangesAsync();

        var round = await db.InspectionDisputes.CountAsync(d => d.BookingId == assignment.BookingId);
        await RealtimeNotify.InspectionCheck(realtime, RealtimeEventTypes.InspectionDisputeRaised,
            RealtimeEntityTypes.InspectionDispute, dispute.InspectionDisputeId, assignment.BookingId, driverId, round);
        return (dispute, null);
    }

    /// <summary>"Nhắc điều phối": the driver is waiting for the pre-handover record.</summary>
    public async Task<string?> RemindDispatcherAsync(int driverId, int assignmentId)
    {
        var assignment = await db.TripAssignments
            .FirstOrDefaultAsync(t => t.AssignmentId == assignmentId && t.DriverId == driverId);
        if (assignment is null || assignment.Status is not (TripAssignmentStatuses.Assigned or TripAssignmentStatuses.Accepted))
            return TripNotWaitingToStart;
        if (await inspections.GetPreHandoverAsync(assignment.BookingId) is not null)
            return AlreadyHasPreHandover;

        await RealtimeNotify.InspectionCheck(realtime, RealtimeEventTypes.DispatcherReminded,
            RealtimeEntityTypes.TripAssignment, assignment.AssignmentId, assignment.BookingId, driverId, 1);
        return null;
    }

    private Task<bool> HasOpenDisputeAsync(int bookingId)
        => db.InspectionDisputes.AnyAsync(d => d.BookingId == bookingId && d.Status == InspectionDisputeStatuses.Open);

    public async Task<bool> StartTripAsync(int driverId, int assignmentId)
    {
        var (ok, _) = await StartTripAsync(driverId, assignmentId, null);
        return ok;
    }

    public async Task<(bool Ok, string? Error)> StartTripAsync(
        int driverId, int assignmentId, VehicleConditionRequest? request)
    {
        var assignment = await db.TripAssignments
            .Include(t => t.Booking)
            .FirstOrDefaultAsync(t => t.AssignmentId == assignmentId && t.DriverId == driverId);

        if (assignment is null || assignment.Status != TripAssignmentStatuses.Accepted)
            return (false, null);
        if (!BookingStateTransitionRules.CanTransition(assignment.Booking.Status, BookingStatuses.InProgress))
            return (false, BookingStateTransitionRules.InvalidTransition);

        // Two-sided check (plan1.md Phần A): the dispatcher records the car first, the driver confirms
        // it matches the real car. Starting = confirming; a mismatch goes through RaiseDisputeAsync instead.
        var preHandover = await inspections.GetPreHandoverAsync(assignment.BookingId);
        if (preHandover is null)
            return (false, PreHandoverMissing);
        if (await HasOpenDisputeAsync(assignment.BookingId))
            return (false, DisputeOpen);

        // The driver's own numbers when sent; otherwise they accepted the dispatcher's record as it is.
        var source = request ?? new VehicleConditionRequest(
            preHandover.OdometerKm, preHandover.FuelLevel, preHandover.Condition, preHandover.Notes,
            preHandover.ExteriorCondition, preHandover.TechnicalCondition);
        var (inspection, error) = await inspections.AddAsync(
            assignment.BookingId,
            VehicleInspectionTypes.Handover,
            source.OdometerKm,
            source.FuelLevel,
            source.Condition,
            source.Notes,
            source.ExteriorCondition,
            source.TechnicalCondition,
            driverId);
        if (inspection is null)
            return (false, error ?? "Không thể lưu biên bản kiểm tra đầu chuyến.");

        assignment.Status = TripAssignmentStatuses.InProgress;
        assignment.StartedAt = DateTime.UtcNow;
        assignment.Booking.Status = BookingStatuses.InProgress;
        assignment.Booking.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await InteractionLog.TryRecordAsync(
            db, assignment.Booking.CustomerId, assignment.VehicleId, InteractionLog.Rent, assignment.BookingId);
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
        return (true, null);
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
        var returnAt = DateTime.UtcNow;
        var scheduleBlock = await schedule.GetLateReturnScheduleBlockAsync(
            assignment.Booking, returnAt, assignment.VehicleId, assignment.DriverId);
        if (scheduleBlock is not null)
            return (false, scheduleBlock);

        var (inspection, inspectError) = await inspections.AddAsync(
            assignment.BookingId,
            VehicleInspectionTypes.Return,
            request?.OdometerKm,
            request?.FuelLevel,
            request?.Condition,
            request?.Notes,
            request?.ExteriorCondition,
            request?.TechnicalCondition,
            driverId);
        if (inspection is null)
            return (false, inspectError ?? "Không thể ghi nhận trả xe.");
        // The trip completes now; the dispatcher checks the driver's return report afterwards (plan1.md Phần A).
        inspection.VerificationStatus = InspectionVerificationStatuses.Pending;

        var actualKm = VehicleInspectionRules.ResolveActualKm(handover?.OdometerKm, request?.OdometerKm);
        await fees.ApplyCompletionAsync(assignment.Booking, actualKm, handover, inspection);

        assignment.Status = TripAssignmentStatuses.Completed;
        assignment.CompletedAt = DateTime.UtcNow;
        assignment.Booking.Status = BookingStatuses.Completed;
        assignment.Booking.UpdatedAt = DateTime.UtcNow;
        assignment.Driver.TotalTrips += 1;
        // Another Assigned/InProgress booking may still hold this car; only release when idle.
        await VehicleOccupancy.ReleaseIfIdleAsync(db, assignment.VehicleId, assignment.BookingId);

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
            out var fullName, out var email, out var phone)
            ?? ValidateLicence(request.LicenseClass, request.LicenseNumber);
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
            // No made-up licence: empty until entered; the driver cannot be assigned before it is verified.
            LicenseNumber = request.LicenseNumber?.Trim().ToUpperInvariant() ?? string.Empty,
            LicenseExpiry = request.LicenseExpiry ?? DateOnly.MinValue,
            Status = DriverStatuses.Available,
            AverageRating = 0,
            TotalTrips = 0,
            IsActive = true,
            LicenseClass = TrimOrNull(request.LicenseClass),
            LicenseIssueDate = request.LicenseIssueDate,
            IdNumber = TrimOrNull(request.IdNumber),
            DateOfBirth = request.DateOfBirth,
            Address = TrimOrNull(request.Address),
            AvatarUrl = TrimOrNull(request.AvatarUrl),
            ExperienceYears = request.ExperienceYears,
            OnTimeRate = request.OnTimeRate,
            TotalDistanceKm = request.TotalDistanceKm,
            VerificationStatus = NormalizeVerification(request.VerificationStatus) ?? DriverVerificationStatuses.Pending
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
        if (ValidateLicence(request.LicenseClass, request.LicenseNumber) is string licenceError)
            return (null, licenceError, StatusCodes.Status400BadRequest);

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

        ApplyProfile(driver, request);
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
        driver.TotalTrips, driver.IsActive,
        driver.LicenseClass, driver.LicenseIssueDate, driver.IdNumber, driver.DateOfBirth,
        driver.Address, driver.AvatarUrl, driver.ExperienceYears, driver.OnTimeRate,
        driver.TotalDistanceKm, driver.VerificationStatus);

    private static string? ValidateLicence(string? licenceClass, string? licenceNumber)
    {
        if (!string.IsNullOrWhiteSpace(licenceClass) && Validation.LicenseClasses.Find(licenceClass) is null)
            return DriverVerificationService.LicenseClassInvalid;
        if (licenceNumber is { Length: > 30 })
            return DriverVerificationService.LicenseNumberTooLong;
        return null;
    }

    private static string? NormalizeVerification(string? value) => TrimOrNull(value) switch
    {
        null => null,
        var v when string.Equals(v, DriverVerificationStatuses.Verified, StringComparison.OrdinalIgnoreCase) => DriverVerificationStatuses.Verified,
        var v when string.Equals(v, DriverVerificationStatuses.Rejected, StringComparison.OrdinalIgnoreCase) => DriverVerificationStatuses.Rejected,
        _ => DriverVerificationStatuses.Pending
    };

    private static void ApplyProfile(Driver driver, UpdateAdminDriverRequest request)
    {
        if (request.LicenseNumber is not null)
            driver.LicenseNumber = request.LicenseNumber.Trim().ToUpperInvariant();
        if (request.LicenseExpiry is not null)
            driver.LicenseExpiry = request.LicenseExpiry.Value;
        if (request.LicenseClass is not null)
            driver.LicenseClass = TrimOrNull(request.LicenseClass);
        if (request.LicenseIssueDate is not null)
            driver.LicenseIssueDate = request.LicenseIssueDate;
        if (request.IdNumber is not null)
            driver.IdNumber = TrimOrNull(request.IdNumber);
        if (request.DateOfBirth is not null)
            driver.DateOfBirth = request.DateOfBirth;
        if (request.Address is not null)
            driver.Address = TrimOrNull(request.Address);
        if (request.AvatarUrl is not null)
            driver.AvatarUrl = TrimOrNull(request.AvatarUrl);
        if (request.ExperienceYears is not null)
            driver.ExperienceYears = request.ExperienceYears;
        if (request.OnTimeRate is not null)
            driver.OnTimeRate = request.OnTimeRate;
        if (request.TotalDistanceKm is not null)
            driver.TotalDistanceKm = request.TotalDistanceKm;

        var status = NormalizeVerification(request.VerificationStatus);
        if (status is not null)
            driver.VerificationStatus = status;
    }

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
