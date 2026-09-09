using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.DTOs.Dispatch;
using Backend.DTOs.Drivers;
using Backend.DTOs.Vehicles;
using Backend.Entities;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class DispatchService(
    CarRentalDbContext db,
    BookingService bookingService,
    DriverService driverService,
    VehicleInspectionService inspections,
    BookingFeeService fees,
    ScheduleConflictService schedule,
    IRealtimePublisher? realtime = null)
{
    public async Task<(BookingResponse? Booking, string? Error)> ConfirmBookingAsync(int bookingId, int dispatcherId)
    {
        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is null)
            return Fail("Không tìm thấy đơn.");
        if (!BookingStateTransitionRules.CanConfirm(booking.Status))
            return Fail("Chỉ xác nhận đơn đang chờ.");

        var (result, error, _) = await bookingService.ConfirmPendingAsync(
            bookingId, dispatcherId, "Điều phối xác nhận đơn");
        return result is null ? Fail(error ?? "Không thể xác nhận đơn.") : Ok(result);
    }

    public async Task<DispatchFleetStatusResponse> GetFleetStatusAsync()
    {
        var vehicles = await db.Vehicles.AsNoTracking()
            .Include(v => v.VehicleType)
            .OrderBy(v => v.LicensePlate)
            .ToListAsync();
        var occupying = await schedule.ListOccupyingBookingsAsync();
        var openAssignments = await db.TripAssignments.AsNoTracking()
            .Include(t => t.Driver).ThenInclude(d => d.User)
            .Include(t => t.Booking)
            .Include(t => t.Vehicle)
            .Where(t =>
                t.Status == TripAssignmentStatuses.Assigned
                || t.Status == TripAssignmentStatuses.Accepted
                || t.Status == TripAssignmentStatuses.InProgress)
            .ToListAsync();

        var vehicleRows = vehicles.Select(vehicle =>
        {
            var booking = occupying
                .Where(b =>
                    b.AssignedVehicleId == vehicle.VehicleId
                    || (b.TripAssignment != null && b.TripAssignment.VehicleId == vehicle.VehicleId))
                .OrderBy(b => OccupancyRank(b.Status))
                .ThenBy(b => b.StartDate)
                .FirstOrDefault();
            var assignment = openAssignments.FirstOrDefault(t => t.VehicleId == vehicle.VehicleId)
                ?? booking?.TripAssignment;
            var rentalMode = booking is null ? null : ResolveMode(booking.RentalMode);
            var withDriver = rentalMode == RentalModes.WithDriver;
            return new DispatchVehicleStatusResponse(
                vehicle.VehicleId,
                vehicle.LicensePlate,
                vehicle.VehicleType?.TypeName ?? string.Empty,
                vehicle.Status,
                withDriver ? assignment?.Driver?.User?.FullName : null,
                withDriver ? assignment?.Driver?.Status : null,
                booking?.BookingId,
                withDriver ? assignment?.AssignmentId : null,
                booking?.StartDate,
                booking?.EndDate,
                rentalMode);
        }).ToList();

        var drivers = await db.Drivers.AsNoTracking()
            .Include(d => d.User)
            .OrderBy(d => d.User.FullName)
            .ToListAsync();
        var driverRows = drivers.Select(driver =>
        {
            var assignment = openAssignments
                .Where(t => t.DriverId == driver.DriverId)
                .OrderBy(t => OccupancyRank(t.Booking?.Status))
                .ThenBy(t => t.Booking?.StartDate ?? DateTime.MaxValue)
                .FirstOrDefault();
            return new DispatchDriverStatusResponse(
                driver.DriverId,
                driver.User.FullName,
                driver.Status,
                driver.IsActive,
                assignment?.Vehicle?.LicensePlate,
                assignment?.BookingId,
                assignment?.Booking?.StartDate,
                assignment?.Booking?.EndDate,
                assignment?.Booking is null ? null : ResolveMode(assignment.Booking.RentalMode));
        }).ToList();

        return new DispatchFleetStatusResponse(vehicleRows, driverRows);
    }

    public async Task<(DispatchAssignableResponse? Data, string? Error)> GetAssignableAsync(int bookingId)
    {
        var booking = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking is null)
            return (null, "Không tìm thấy đơn.");
        if (booking.Status != BookingStatuses.Confirmed)
            return (null, "Chỉ lấy danh sách khả dụng cho đơn đã xác nhận.");

        var vehicles = await schedule.FindAssignableVehiclesAsync(
            booking.VehicleTypeId, booking.StartDate, booking.EndDate, excludeBookingId: booking.BookingId);
        var vehicleRows = vehicles.Select(v => new VehicleResponse(
            v.VehicleId, v.TypeId, v.VehicleType.TypeName, v.LicensePlate,
            v.Brand, v.Model, v.Year, v.Color, v.Status, v.CurrentKm,
            v.RegistrationNumber, v.RegistrationExpiryDate,
            v.InspectionExpiryDate, v.InsuranceExpiryDate)).ToList();

        var driverRows = new List<DriverResponse>();
        if (ResolveMode(booking.RentalMode) == RentalModes.WithDriver)
        {
            var drivers = await schedule.FindAssignableDriversAsync(
                booking.StartDate, booking.EndDate, excludeBookingId: booking.BookingId);
            foreach (var driver in drivers)
            {
                if (await driverService.HasOpenAssignmentAsync(driver.DriverId))
                    continue;
                driverRows.Add(new DriverResponse(
                    driver.DriverId,
                    driver.User.FullName,
                    driver.User.Email,
                    driver.User.Phone,
                    driver.LicenseNumber,
                    driver.LicenseExpiry,
                    driver.Status,
                    driver.AverageRating,
                    driver.TotalTrips));
            }
        }

        return (new DispatchAssignableResponse(vehicleRows, driverRows), null);
    }

    public Task<AssignTripResult> AssignTripAsync(
        int bookingId, AssignTripRequest request, int dispatcherId)
        => SqliteWriteLock.ExecuteAsync(db, () => AssignTripCoreAsync(bookingId, request, dispatcherId));

    private async Task<AssignTripResult> AssignTripCoreAsync(
        int bookingId, AssignTripRequest request, int dispatcherId)
    {
        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is null)
            return AssignTripResult.Fail("Không tìm thấy đơn.");
        if (!BookingStateTransitionRules.CanTransition(booking.Status, BookingStatuses.Assigned))
        {
            return booking.Status is BookingStatuses.Completed or BookingStatuses.Cancelled
                ? AssignTripResult.Fail("Đơn đã kết thúc, không thể phân công.")
                : AssignTripResult.Fail("Đơn không thể phân công.");
        }

        var readiness = await BookingDispatchReadinessRules.GetBlockReasonAsync(db, bookingId);
        if (readiness is not null)
            return AssignTripResult.Fail(readiness);

        if (request.VehicleId <= 0)
            return AssignTripResult.Fail("Cần chọn xe.");

        var rentalMode = ResolveMode(booking.RentalMode);
        return rentalMode == RentalModes.SelfDrive
            ? await AssignSelfDriveAsync(booking, request, dispatcherId)
            : await AssignWithDriverAsync(booking, request, dispatcherId);
    }

    public async Task<(BookingResponse? Booking, string? Error)> HandoverSelfDriveAsync(
        int bookingId, int dispatcherId, VehicleConditionRequest? request)
    {
        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is null)
            return Fail("Không tìm thấy đơn.");
        if (ResolveMode(booking.RentalMode) != RentalModes.SelfDrive)
            return Fail("Chỉ đơn tự lái mới dùng giao xe.");
        if (!BookingStateTransitionRules.CanTransition(booking.Status, BookingStatuses.InProgress))
            return Fail("Chỉ giao xe khi đơn đã được gán xe.");
        if (booking.AssignedVehicleId is null)
            return Fail("Đơn chưa được gán xe.");

        var readiness = await BookingDispatchReadinessRules.GetBlockReasonAsync(db, bookingId);
        if (readiness is not null)
            return Fail(readiness);

        var (inspection, inspectError) = await inspections.AddAsync(
            bookingId,
            VehicleInspectionTypes.Handover,
            request?.OdometerKm,
            request?.FuelLevel,
            request?.Condition,
            request?.Notes,
            request?.ExteriorCondition,
            request?.TechnicalCondition);
        if (inspection is null)
            return Fail(inspectError ?? "Không thể ghi nhận giao xe.");

        var oldStatus = booking.Status;
        booking.Status = BookingStatuses.InProgress;
        booking.UpdatedAt = DateTime.UtcNow;
        AddHistory(bookingId, oldStatus, BookingStatuses.InProgress, dispatcherId, "Điều phối giao xe tự lái");

        await db.SaveChangesAsync();
        var result = await bookingService.GetBookingByIdAsync(bookingId);
        if (result is not null)
            await RealtimeNotify.BookingStatusChanged(
                realtime, result.CustomerId, result.BookingId, result.Status, result.AssignedVehicle?.VehicleId);
        return result is null ? Fail("Không thể giao xe.") : Ok(result);
    }

    public async Task<(BookingResponse? Booking, string? Error)> CompleteSelfDriveAsync(
        int bookingId, int dispatcherId, VehicleConditionRequest? request)
    {
        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is null)
            return Fail("Không tìm thấy đơn.");
        if (ResolveMode(booking.RentalMode) != RentalModes.SelfDrive)
            return Fail("Chỉ đơn tự lái mới hoàn thành trả xe tại điều phối.");
        if (!BookingStateTransitionRules.CanTransition(booking.Status, BookingStatuses.Completed))
            return Fail("Chỉ hoàn thành khi đơn đang trong quá trình thuê.");
        if (booking.AssignedVehicleId is null)
            return Fail("Đơn chưa được gán xe.");

        var vehicle = await db.Vehicles.FindAsync(booking.AssignedVehicleId.Value);
        if (vehicle is null)
            return Fail("Không tìm thấy xe đã gán.");

        var handover = await inspections.GetHandoverAsync(bookingId);
        if (handover is null)
            return Fail(VehicleInspectionRules.HandoverRequired);

        var (inspection, inspectError) = await inspections.AddAsync(
            bookingId,
            VehicleInspectionTypes.Return,
            request?.OdometerKm,
            request?.FuelLevel,
            request?.Condition,
            request?.Notes,
            request?.ExteriorCondition,
            request?.TechnicalCondition);
        if (inspection is null)
            return Fail(inspectError ?? "Không thể ghi nhận trả xe.");

        var actualKm = VehicleInspectionRules.ResolveActualKm(handover?.OdometerKm, request?.OdometerKm);
        await fees.ApplyCompletionAsync(booking, actualKm, handover, inspection);

        var oldStatus = booking.Status;
        booking.Status = BookingStatuses.Completed;
        booking.UpdatedAt = DateTime.UtcNow;
        if (vehicle.Status == VehicleStatuses.Rented)
            vehicle.Status = VehicleStatuses.Available;
        AddHistory(bookingId, oldStatus, BookingStatuses.Completed, dispatcherId, "Điều phối hoàn thành trả xe tự lái");

        await db.SaveChangesAsync();
        var result = await bookingService.GetBookingByIdAsync(bookingId);
        if (result is not null)
        {
            await RealtimeNotify.BookingStatusChanged(
                realtime, result.CustomerId, result.BookingId, result.Status, result.AssignedVehicle?.VehicleId);
            await RealtimeNotify.VehicleStatusChanged(
                realtime, vehicle.VehicleId, vehicle.Status, bookingId, result.CustomerId);
        }
        return result is null ? Fail("Không thể hoàn thành đơn.") : Ok(result);
    }

    private async Task<AssignTripResult> AssignWithDriverAsync(
        Booking booking, AssignTripRequest request, int dispatcherId)
    {
        if (request.DriverId is null or <= 0)
            return AssignTripResult.Fail("Đơn có tài xế bắt buộc chọn tài xế.");

        if (await db.TripAssignments.AnyAsync(t => t.BookingId == booking.BookingId))
            return AssignTripResult.Fail("Đơn đã được phân công tài xế và xe.");

        var (vehicle, vehicleFail) = await LoadAssignableVehicleAsync(request.VehicleId, booking.VehicleTypeId);
        if (vehicle is null)
            return vehicleFail ?? AssignTripResult.Fail("Xe không khả dụng.");
        if (await schedule.HasVehicleConflictAsync(
            request.VehicleId, booking.StartDate, booking.EndDate, booking.BookingId))
        {
            return AssignTripResult.FromConflict(await BuildConflictAsync(
                booking,
                "Xe đã có lịch thuê khác trong khoảng thời gian này.",
                ScheduleConflictTypes.Vehicle,
                excludeVehicleId: request.VehicleId,
                excludeDriverId: null));
        }

        var driver = await db.Drivers
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DriverId == request.DriverId && d.IsActive && d.Status == DriverStatuses.Available);
        if (driver is null)
            return AssignTripResult.Fail("Tài xế không khả dụng.");
        if (await driverService.HasOpenAssignmentAsync(request.DriverId.Value))
        {
            return AssignTripResult.FromConflict(await BuildConflictAsync(
                booking,
                "Tài xế đang có chuyến chưa hoàn thành.",
                ScheduleConflictTypes.Driver,
                excludeVehicleId: null,
                excludeDriverId: request.DriverId.Value));
        }

        if (await schedule.HasDriverConflictAsync(
            request.DriverId.Value, booking.StartDate, booking.EndDate, booking.BookingId))
        {
            return AssignTripResult.FromConflict(await BuildConflictAsync(
                booking,
                "Tài xế đã có lịch chuyến khác trong khoảng thời gian này.",
                ScheduleConflictTypes.Driver,
                excludeVehicleId: null,
                excludeDriverId: request.DriverId.Value));
        }

        db.TripAssignments.Add(new TripAssignment
        {
            BookingId = booking.BookingId,
            DriverId = request.DriverId.Value,
            VehicleId = request.VehicleId,
            AssignedBy = dispatcherId,
            AssignedAt = DateTime.UtcNow,
            Status = TripAssignmentStatuses.Assigned
        });

        var oldStatus = booking.Status;
        if (booking.AssignedVehicleId is not null)
            booking.AssignedVehicleId = request.VehicleId;
        driver.Status = DriverStatuses.Busy;
        vehicle.Status = VehicleStatuses.Rented;
        booking.Status = BookingStatuses.Assigned;
        booking.UpdatedAt = DateTime.UtcNow;
        AddHistory(
            booking.BookingId, oldStatus, BookingStatuses.Assigned, dispatcherId,
            $"Phân công tài xế {driver.User.FullName}");

        await db.SaveChangesAsync();
        var result = await bookingService.GetBookingByIdAsync(booking.BookingId);
        if (result is not null)
        {
            var assignmentId = result.Assignment?.AssignmentId;
            await RealtimeNotify.AssignmentChanged(
                realtime, result.CustomerId, result.BookingId, assignmentId,
                request.VehicleId, request.DriverId, result.Status);
            await RealtimeNotify.BookingStatusChanged(
                realtime, result.CustomerId, result.BookingId, result.Status,
                result.AssignedVehicle?.VehicleId, request.DriverId);
            await RealtimeNotify.VehicleStatusChanged(
                realtime, vehicle.VehicleId, vehicle.Status, result.BookingId, result.CustomerId, request.DriverId);
            await RealtimeNotify.DriverStatusChanged(realtime, request.DriverId.Value, driver.Status);
        }
        return result is null
            ? AssignTripResult.Fail("Không thể phân công chuyến đi.")
            : AssignTripResult.Ok(result);
    }

    private async Task<AssignTripResult> AssignSelfDriveAsync(
        Booking booking, AssignTripRequest request, int dispatcherId)
    {
        if (request.DriverId is > 0)
            return AssignTripResult.Fail("Đơn tự lái không được gán tài xế.");
        if (await db.TripAssignments.AnyAsync(t => t.BookingId == booking.BookingId))
            return AssignTripResult.Fail("Đơn đã có phân công chuyến.");

        var (vehicle, vehicleFail) = await LoadAssignableVehicleAsync(request.VehicleId, booking.VehicleTypeId);
        if (vehicle is null)
            return vehicleFail ?? AssignTripResult.Fail("Xe không khả dụng.");
        if (await schedule.HasVehicleConflictAsync(
            request.VehicleId, booking.StartDate, booking.EndDate, booking.BookingId))
        {
            return AssignTripResult.FromConflict(await BuildConflictAsync(
                booking,
                "Xe đã có lịch thuê khác trong khoảng thời gian này.",
                ScheduleConflictTypes.Vehicle,
                excludeVehicleId: request.VehicleId,
                excludeDriverId: null));
        }

        var oldStatus = booking.Status;
        booking.AssignedVehicleId = vehicle.VehicleId;
        vehicle.Status = VehicleStatuses.Rented;
        booking.Status = BookingStatuses.Assigned;
        booking.UpdatedAt = DateTime.UtcNow;
        AddHistory(
            booking.BookingId, oldStatus, BookingStatuses.Assigned, dispatcherId,
            $"Gán xe {vehicle.LicensePlate} (tự lái)");

        await db.SaveChangesAsync();
        var result = await bookingService.GetBookingByIdAsync(booking.BookingId);
        if (result is not null)
        {
            await RealtimeNotify.AssignmentChanged(
                realtime, result.CustomerId, result.BookingId, null,
                vehicle.VehicleId, null, result.Status);
            await RealtimeNotify.BookingStatusChanged(
                realtime, result.CustomerId, result.BookingId, result.Status, vehicle.VehicleId);
            await RealtimeNotify.VehicleStatusChanged(
                realtime, vehicle.VehicleId, vehicle.Status, result.BookingId, result.CustomerId);
        }
        return result is null
            ? AssignTripResult.Fail("Không thể gán xe.")
            : AssignTripResult.Ok(result);
    }

    private async Task<(Vehicle? Vehicle, AssignTripResult? Fail)> LoadAssignableVehicleAsync(
        int vehicleId, int expectedTypeId)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
        if (vehicle is null)
            return (null, AssignTripResult.Fail("Xe không khả dụng."));

        var block = await schedule.GetMaintenanceNewScheduleBlockReasonAsync(vehicle.VehicleId);
        if (block is not null)
            return (null, AssignTripResult.Fail(block));
        if (vehicle.Status != VehicleStatuses.Available)
            return (null, AssignTripResult.Fail("Xe không khả dụng."));
        if (vehicle.TypeId != expectedTypeId)
            return (null, AssignTripResult.Fail("Xe không thuộc loại xe được đặt."));

        return (vehicle, null);
    }

    private async Task<AssignConflictResponse> BuildConflictAsync(
        Booking booking,
        string message,
        string conflictType,
        int? excludeVehicleId,
        int? excludeDriverId)
    {
        var vehicles = await schedule.FindAssignableVehiclesAsync(
            booking.VehicleTypeId, booking.StartDate, booking.EndDate, excludeVehicleId, booking.BookingId);
        var vehicleAlts = vehicles
            .Select(v => new VehicleAlternativeResponse(
                v.VehicleId, v.LicensePlate, v.Brand, v.Model, v.Status))
            .ToList();

        var driverAlts = new List<DriverAlternativeResponse>();
        if (ResolveMode(booking.RentalMode) == RentalModes.WithDriver)
        {
            var drivers = await schedule.FindAssignableDriversAsync(
                booking.StartDate, booking.EndDate, excludeDriverId, booking.BookingId);
            foreach (var driver in drivers)
            {
                if (await driverService.HasOpenAssignmentAsync(driver.DriverId))
                    continue;
                driverAlts.Add(new DriverAlternativeResponse(
                    driver.DriverId, driver.User.FullName, driver.User.Phone, driver.Status));
            }
        }

        return new AssignConflictResponse(message, conflictType, vehicleAlts, driverAlts);
    }

    private void AddHistory(int bookingId, string? oldStatus, string newStatus, int changedBy, string note)
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
    }

    private static int OccupancyRank(string? status) => status switch
    {
        BookingStatuses.InProgress => 0,
        BookingStatuses.Assigned => 1,
        BookingStatuses.Confirmed => 2,
        BookingStatuses.Pending => 3,
        _ => 9
    };

    private static string ResolveMode(string? rentalMode)
        => RentalModes.TryResolve(rentalMode, out var resolved) ? resolved : RentalModes.WithDriver;

    private static (BookingResponse? Booking, string? Error) Ok(BookingResponse booking) => (booking, null);
    private static (BookingResponse? Booking, string? Error) Fail(string error) => (null, error);
}
