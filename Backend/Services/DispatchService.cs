using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.DTOs.Dispatch;
using Backend.DTOs.Drivers;
using Backend.DTOs.Realtime;
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
    public async Task<AssignTripResult> ConfirmBookingAsync(int bookingId, int dispatcherId)
    {
        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is null)
            return AssignTripResult.Fail("Không tìm thấy đơn.");
        if (!BookingStateTransitionRules.CanConfirm(booking.Status))
            return AssignTripResult.Fail("Chỉ xác nhận đơn đang chờ.");

        var dateError = BookingDateRules.ValidateNewRental(booking.StartDate, booking.EndDate);
        if (dateError is not null)
            return AssignTripResult.Fail(dateError);

        if (ResolveMode(booking.RentalMode) == RentalModes.SelfDrive)
        {
            // plan1.md Phần E: self-drive is confirmed only once the customer's licence is approved.
            var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == booking.CustomerId);
            var type = await db.VehicleTypes.AsNoTracking().FirstOrDefaultAsync(t => t.TypeId == booking.VehicleTypeId);
            if (customer is not null && type is not null
                && CustomerLicenseService.ConfirmBlockReason(customer, type, booking.EndDate) is string licenceBlock)
                return AssignTripResult.Fail(licenceBlock);
        }

        if (booking.AssignedVehicleId is int vehicleId and > 0)
        {
            var (vehicle, vehicleFail) = await LoadAssignableVehicleAsync(vehicleId, booking.VehicleTypeId);
            if (vehicle is null)
                return vehicleFail ?? AssignTripResult.Fail("Xe không khả dụng.");

            if (await schedule.HasVehicleConflictAsync(
                vehicleId, booking.StartDate, booking.EndDate, booking.BookingId))
            {
                return AssignTripResult.FromConflict(await BuildConflictAsync(
                    booking,
                    "Xe đã có lịch thuê khác trong khoảng thời gian này.",
                    ScheduleConflictTypes.Vehicle,
                    excludeVehicleId: vehicleId,
                    excludeDriverId: null));
            }
        }

        if (ResolveMode(booking.RentalMode) == RentalModes.WithDriver)
        {
            var hasDriver = await HasConfirmableDriverAsync(booking);
            if (!hasDriver)
            {
                return AssignTripResult.FromConflict(await BuildConflictAsync(
                    booking,
                    "Không có tài xế khả dụng trong khoảng thời gian này.",
                    ScheduleConflictTypes.Driver,
                    excludeVehicleId: null,
                    excludeDriverId: null));
            }
        }

        var (result, error, _) = await bookingService.ConfirmPendingAsync(
            bookingId, dispatcherId, "Điều phối xác nhận đơn");
        return result is null
            ? AssignTripResult.Fail(error ?? "Không thể xác nhận đơn.")
            : AssignTripResult.Ok(result);
    }

    private async Task<bool> HasConfirmableDriverAsync(Booking booking)
    {
        var drivers = await schedule.FindAssignableDriversAsync(
            booking.StartDate, booking.EndDate, excludeBookingId: booking.BookingId,
            qualifyFor: await db.VehicleTypes.FindAsync(booking.VehicleTypeId));
        foreach (var driver in drivers)
        {
            if (await driverService.HasOpenAssignmentAsync(driver.DriverId))
                continue;
            return true;
        }

        return false;
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
        if (booking.Status is not (BookingStatuses.Pending or BookingStatuses.Confirmed))
            return (null, "Chỉ xem khả năng đáp ứng cho đơn chờ xác nhận hoặc đã xác nhận.");

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
                booking.StartDate, booking.EndDate, excludeBookingId: booking.BookingId,
                qualifyFor: await db.VehicleTypes.FindAsync(booking.VehicleTypeId));
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
        var booking = await db.Bookings
            .Include(b => b.TripAssignment)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking is null)
            return Fail("Không tìm thấy đơn.");
        // With-driver trips start from the driver's app after they confirm the dispatcher's pre-handover check.
        if (ResolveMode(booking.RentalMode) == RentalModes.WithDriver)
            return Fail(WithDriverUsesPreHandover);
        if (!BookingStateTransitionRules.CanTransition(booking.Status, BookingStatuses.InProgress))
            return Fail("Chỉ giao xe khi đơn đã được gán xe.");

        var vehicleId = booking.AssignedVehicleId ?? booking.TripAssignment?.VehicleId;
        if (vehicleId is null or <= 0)
            return Fail("Đơn chưa được gán xe.");
        booking.AssignedVehicleId ??= vehicleId;

        var readiness = await BookingDispatchReadinessRules.GetBlockReasonAsync(db, bookingId);
        if (readiness is not null)
            return Fail(readiness);


        // Prefer request fuel; else reuse last known level from vehicle inspections.
        // Do not invent 0 — if neither source has a valid level, inspection rules reject as before.
        var fuelLevel = request?.FuelLevel;
        if (fuelLevel is null)
            fuelLevel = await inspections.GetLatestFuelLevelAsync(vehicleId.Value);

        var (inspection, inspectError) = await inspections.AddAsync(
            bookingId,
            VehicleInspectionTypes.Handover,
            request?.OdometerKm,
            fuelLevel,
            request?.Condition,
            request?.Notes,
            request?.ExteriorCondition,
            request?.TechnicalCondition,
            dispatcherId);
        if (inspection is null)
            return Fail(inspectError ?? "Không thể ghi nhận giao xe.");

        // plan1.md Phần E: the dispatcher checks the customer's original licence before handing over the car
        // (after the report itself is valid; nothing is saved when this fails).
        if (request?.OriginalLicenseChecked != true)
            return Fail(OriginalLicenceNotChecked);
        var licenceChecker = await ActorLookup.FindAsync(db, dispatcherId);
        booking.OriginalLicenseCheckedByUserId = licenceChecker?.UserId;
        booking.OriginalLicenseCheckedByName = licenceChecker?.Name;
        booking.OriginalLicenseCheckedAt = DateTime.UtcNow;

        var oldStatus = booking.Status;
        booking.Status = BookingStatuses.InProgress;
        booking.UpdatedAt = DateTime.UtcNow;
        if (booking.TripAssignment is { } trip
            && trip.Status is TripAssignmentStatuses.Assigned or TripAssignmentStatuses.Accepted)
        {
            trip.Status = TripAssignmentStatuses.InProgress;
            trip.StartedAt ??= DateTime.UtcNow;
        }

        AddHistory(bookingId, oldStatus, BookingStatuses.InProgress, dispatcherId, "Điều phối giao xe");

        await db.SaveChangesAsync();
        await InteractionLog.TryRecordAsync(db, booking.CustomerId, vehicleId.Value, InteractionLog.Rent, bookingId);
        var result = await bookingService.GetBookingByIdAsync(bookingId);
        if (result is not null)
            await RealtimeNotify.BookingStatusChanged(
                realtime, result.CustomerId, result.BookingId, result.Status, result.AssignedVehicle?.VehicleId);
        return result is null ? Fail("Không thể giao xe.") : Ok(result);
    }

    public async Task<(BookingResponse? Booking, string? Error)> CompleteSelfDriveAsync(
        int bookingId, int dispatcherId, VehicleConditionRequest? request)
    {
        var booking = await db.Bookings
            .Include(b => b.TripAssignment)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking is null)
            return Fail("Không tìm thấy đơn.");

        // Dispatcher return: InProgress (normal) or Assigned (stuck/overdue trip never handed over).
        var isInProgress = booking.Status == BookingStatuses.InProgress;
        var isAssignedReturn = booking.Status == BookingStatuses.Assigned;
        if (!isInProgress && !isAssignedReturn)
            return Fail("Chỉ hoàn thành khi đơn đang trong quá trình thuê.");
        if (isInProgress && !BookingStateTransitionRules.CanTransition(booking.Status, BookingStatuses.Completed))
            return Fail("Chỉ hoàn thành khi đơn đang trong quá trình thuê.");

        var vehicleId = booking.AssignedVehicleId ?? booking.TripAssignment?.VehicleId;
        if (vehicleId is null or <= 0)
            return Fail("Đơn chưa được gán xe.");
        booking.AssignedVehicleId ??= vehicleId;

        var vehicle = await db.Vehicles.FindAsync(vehicleId.Value);
        if (vehicle is null)
            return Fail("Không tìm thấy xe đã gán.");

        var handover = await inspections.GetHandoverAsync(bookingId);
        // Normal SelfDrive path after dispatcher handover requires a handover record.
        // Assigned stuck trips (and WithDriver started by driver) may complete with return only.
        if (handover is null
            && isInProgress
            && ResolveMode(booking.RentalMode) == RentalModes.SelfDrive)
            return Fail(VehicleInspectionRules.HandoverRequired);

        var returnAt = DateTime.UtcNow;
        var scheduleBlock = await schedule.GetLateReturnScheduleBlockAsync(
            booking, returnAt, vehicleId, booking.TripAssignment?.DriverId);
        if (scheduleBlock is not null)
            return Fail(scheduleBlock);

        var (inspection, inspectError) = await inspections.AddAsync(
            bookingId,
            VehicleInspectionTypes.Return,
            request?.OdometerKm,
            request?.FuelLevel,
            request?.Condition,
            request?.Notes,
            request?.ExteriorCondition,
            request?.TechnicalCondition,
            dispatcherId);
        if (inspection is null)
            return Fail(inspectError ?? "Không thể ghi nhận trả xe.");

        var actualKm = VehicleInspectionRules.ResolveActualKm(handover?.OdometerKm, request?.OdometerKm);
        await fees.ApplyCompletionAsync(booking, actualKm, handover, inspection);

        var oldStatus = booking.Status;
        booking.Status = BookingStatuses.Completed;
        booking.UpdatedAt = DateTime.UtcNow;

        if (booking.TripAssignment is { } trip
            && trip.Status != TripAssignmentStatuses.Completed
            && trip.Status != TripAssignmentStatuses.Cancelled)
        {
            trip.Status = TripAssignmentStatuses.Completed;
            trip.CompletedAt = DateTime.UtcNow;
            var driver = await db.Drivers.FindAsync(trip.DriverId);
            if (driver is not null)
            {
                driver.TotalTrips += 1;
                if (driver.Status != DriverStatuses.Offline)
                {
                    var stillOpen = await driverService.HasOpenAssignmentAsync(
                        driver.DriverId, trip.AssignmentId);
                    driver.Status = stillOpen ? DriverStatuses.Busy : DriverStatuses.Available;
                }
            }
        }

        await VehicleOccupancy.ReleaseIfIdleAsync(db, vehicle.VehicleId, booking.BookingId);

        AddHistory(bookingId, oldStatus, BookingStatuses.Completed, dispatcherId, "Điều phối tiếp nhận trả xe");

        await db.SaveChangesAsync();
        var result = await bookingService.GetBookingByIdAsync(bookingId);
        if (result is not null)
        {
            await RealtimeNotify.BookingStatusChanged(
                realtime, result.CustomerId, result.BookingId, result.Status, result.AssignedVehicle?.VehicleId);
            await RealtimeNotify.VehicleStatusChanged(
                realtime, vehicle.VehicleId, vehicle.Status, bookingId, result.CustomerId);
            if (booking.TripAssignment is not null)
                await RealtimeNotify.DriverStatusChanged(
                    realtime, booking.TripAssignment.DriverId,
                    (await db.Drivers.FindAsync(booking.TripAssignment.DriverId))?.Status
                        ?? DriverStatuses.Available);
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
        var vehicleType = await db.VehicleTypes.FindAsync(booking.VehicleTypeId);
        if (vehicleType is not null && DriverQualification.ReasonFor(driver, vehicleType, booking.EndDate) is string notQualified)
            return AssignTripResult.Fail(notQualified);
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
        // Rented = currently on a trip; future bookings still use calendar + 2h buffer.
        if (vehicle.Status is VehicleStatuses.Inactive or VehicleStatuses.Maintenance)
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
                booking.StartDate, booking.EndDate, excludeDriverId, booking.BookingId,
                await db.VehicleTypes.FindAsync(booking.VehicleTypeId));
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

    // ---- Two-sided vehicle check on with-driver trips (plan1.md Phần A) ----

    public const string WithDriverUsesPreHandover =
        "Đơn có tài xế: điều phối ghi \"Kiểm tra trước giao\", tài xế xác nhận trên app để bắt đầu chuyến.";
    public const string OriginalLicenceNotChecked =
        "Vui lòng kiểm tra bằng lái gốc của khách (đối chiếu thông tin và ảnh đã duyệt) rồi tick \"Đã kiểm tra bằng lái\". Không khớp hoặc hết hạn thì không giao xe.";
    public const string PreHandoverSelfDrive = "Đơn tự lái giao xe trực tiếp cho khách, không cần kiểm tra trước giao.";
    public const string PreHandoverNotAssigned = "Chỉ ghi kiểm tra trước giao khi đơn đã phân công tài xế và chuyến chưa bắt đầu.";
    public const string PreHandoverLocked = "Tài xế đã xác nhận biên bản trước giao, không sửa được nữa.";
    public const string DisputeNotFound = "Không tìm thấy báo sai lệch.";
    public const string DisputeAlreadyResolved = "Báo sai lệch này đã được giải quyết.";
    public const string ReturnNotPending = "Phiếu trả này không ở trạng thái chờ xác nhận.";
    public const string ReturnResultInvalid = "Kết quả đối chiếu không hợp lệ.";
    public const string MismatchNoteRequired = "Vui lòng ghi rõ điểm lệch.";
    public const string NoteTooLong = "Ghi chú tối đa 500 ký tự.";

    /// <summary>
    /// Dispatcher records the car before a with-driver trip (km, fuel, exterior, technical), or corrects it
    /// after a dispute — only until the driver confirms it. Never starts the trip.
    /// </summary>
    public async Task<(VehicleInspectionResponse? Inspection, string? Error)> SavePreHandoverAsync(
        int bookingId, int dispatcherId, VehicleConditionRequest? request)
    {
        var booking = await db.Bookings
            .Include(b => b.TripAssignment)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking is null)
            return (null, "Không tìm thấy đơn.");
        if (ResolveMode(booking.RentalMode) != RentalModes.WithDriver)
            return (null, PreHandoverSelfDrive);
        if (booking.Status != BookingStatuses.Assigned
            || booking.TripAssignment is not { } trip
            || trip.Status is not (TripAssignmentStatuses.Assigned or TripAssignmentStatuses.Accepted))
            return (null, PreHandoverNotAssigned);

        request ??= new VehicleConditionRequest();
        var existing = await inspections.GetPreHandoverAsync(bookingId);
        if (existing is null)
        {
            var (created, error) = await inspections.AddAsync(
                bookingId, VehicleInspectionTypes.PreHandover, request.OdometerKm, request.FuelLevel,
                request.Condition, request.Notes, request.ExteriorCondition, request.TechnicalCondition, dispatcherId);
            if (created is null)
                return (null, error);
            await db.SaveChangesAsync();
            await RealtimeNotify.InspectionCheck(realtime, RealtimeEventTypes.PreHandoverRecorded,
                RealtimeEntityTypes.Inspection, created.InspectionId, bookingId, trip.DriverId, 1);
            return (VehicleInspectionService.ToResponse(created), null);
        }

        if (await inspections.GetHandoverAsync(bookingId) is not null)
            return (null, PreHandoverLocked);
        var updateError = await inspections.UpdatePreHandoverAsync(existing, request, dispatcherId);
        if (updateError is not null)
            return (null, updateError);
        await db.SaveChangesAsync();
        // The driver is told when the dispatcher marks the dispute resolved (ResolveDisputeAsync).
        return (VehicleInspectionService.ToResponse(existing), null);
    }

    public async Task<(InspectionDisputeResponse? Dispute, string? Error)> ResolveDisputeAsync(
        int disputeId, int dispatcherId, string? note)
    {
        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (text is { Length: > 500 })
            return (null, NoteTooLong);
        var dispute = await db.InspectionDisputes.FirstOrDefaultAsync(d => d.InspectionDisputeId == disputeId);
        if (dispute is null)
            return (null, DisputeNotFound);
        if (dispute.Status != InspectionDisputeStatuses.Open)
            return (null, DisputeAlreadyResolved);

        var actor = await ActorLookup.FindAsync(db, dispatcherId);
        dispute.Status = InspectionDisputeStatuses.Resolved;
        dispute.ResolvedByUserId = actor?.UserId;
        dispute.ResolvedByName = actor?.Name;
        dispute.ResolutionNote = text;
        dispute.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var driverId = await db.TripAssignments
            .Where(t => t.BookingId == dispute.BookingId && t.Status != TripAssignmentStatuses.Cancelled)
            .Select(t => (int?)t.DriverId)
            .FirstOrDefaultAsync();
        if (driverId is int driver)
        {
            var round = await db.InspectionDisputes.CountAsync(d =>
                d.BookingId == dispute.BookingId && d.InspectionDisputeId <= dispute.InspectionDisputeId);
            await RealtimeNotify.InspectionCheck(realtime, RealtimeEventTypes.InspectionDisputeResolved,
                RealtimeEntityTypes.InspectionDispute, dispute.InspectionDisputeId, dispute.BookingId, driver, round);
        }
        return (ToDisputeResponse(dispute), null);
    }

    public async Task<List<InspectionDisputeResponse>> GetDisputesAsync(int? bookingId, string? status)
    {
        var query = db.InspectionDisputes.AsNoTracking().AsQueryable();
        if (bookingId is int id)
            query = query.Where(d => d.BookingId == id);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(d => d.Status == status.Trim());
        var rows = await query.OrderByDescending(d => d.InspectionDisputeId).ToListAsync();
        return rows.Select(ToDisputeResponse).ToList();
    }

    /// <summary>
    /// Dispatcher checks a driver's return report once the car is back: "Confirmed" or "Mismatch" (+ note,
    /// opens an incident). The booking stays Completed and fees are not recalculated (handled manually in v1).
    /// </summary>
    public async Task<(VehicleInspectionResponse? Inspection, string? Error)> VerifyReturnAsync(
        int inspectionId, int dispatcherId, string? result, string? note)
    {
        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (text is { Length: > 500 })
            return (null, NoteTooLong);
        var status = result?.Trim() switch
        {
            InspectionVerificationStatuses.Confirmed => InspectionVerificationStatuses.Confirmed,
            InspectionVerificationStatuses.Mismatch => InspectionVerificationStatuses.Mismatch,
            _ => null
        };
        if (status is null)
            return (null, ReturnResultInvalid);
        if (status == InspectionVerificationStatuses.Mismatch && text is null)
            return (null, MismatchNoteRequired);

        var inspection = await db.VehicleInspections.FirstOrDefaultAsync(i => i.InspectionId == inspectionId);
        if (inspection is null
            || inspection.InspectionType != VehicleInspectionTypes.Return
            || inspection.VerificationStatus != InspectionVerificationStatuses.Pending)
            return (null, ReturnNotPending);

        var actor = await ActorLookup.FindAsync(db, dispatcherId);
        inspection.VerificationStatus = status;
        inspection.VerificationNote = text;
        inspection.VerifiedByUserId = actor?.UserId;
        inspection.VerifiedByName = actor?.Name;
        inspection.VerifiedAt = DateTime.UtcNow;

        if (status == InspectionVerificationStatuses.Mismatch)
        {
            var trip = await db.TripAssignments.AsNoTracking()
                .Where(t => t.BookingId == inspection.BookingId)
                .OrderByDescending(t => t.AssignmentId)
                .FirstOrDefaultAsync();
            if (trip is not null)
            {
                var description = $"Điều phối {actor?.Name ?? "(không rõ)"} đối chiếu phiếu trả xe: có lệch. {text}";
                db.IncidentReports.Add(new IncidentReport
                {
                    BookingId = inspection.BookingId,
                    AssignmentId = trip.AssignmentId,
                    DriverId = trip.DriverId,
                    IncidentType = IncidentTypes.VehicleIssue,
                    Description = description.Length > 500 ? description[..500] : description,
                    OccurredAt = DateTime.UtcNow,
                    Status = IncidentStatuses.Open,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync();
        return (VehicleInspectionService.ToResponse(inspection), null);
    }

    private static InspectionDisputeResponse ToDisputeResponse(InspectionDispute d) => new(
        d.InspectionDisputeId, d.BookingId, d.InspectionId, d.RaisedByName, d.Note, d.Status,
        d.ResolvedByName, d.ResolutionNote, d.ResolvedAt, d.CreatedAt);

    private static string ResolveMode(string? rentalMode)
        => RentalModes.TryResolve(rentalMode, out var resolved) ? resolved : RentalModes.WithDriver;

    private static (BookingResponse? Booking, string? Error) Ok(BookingResponse booking) => (booking, null);
    private static (BookingResponse? Booking, string? Error) Fail(string error) => (null, error);
}
