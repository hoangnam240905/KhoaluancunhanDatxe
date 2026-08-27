using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class DispatchService(
    CarRentalDbContext db,
    BookingService bookingService,
    DriverService driverService,
    VehicleInspectionService inspections,
    BookingFeeService fees)
{
    public async Task<(BookingResponse? Booking, string? Error)> ConfirmBookingAsync(int bookingId, int dispatcherId)
    {
        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is null)
            return Fail("Không tìm thấy đơn.");
        if (booking.Status != BookingStatuses.Pending)
            return Fail("Chỉ xác nhận đơn đang chờ.");

        var result = await bookingService.UpdateStatusAsync(
            bookingId, BookingStatuses.Confirmed, dispatcherId, "Điều phối xác nhận đơn");
        return result is null ? Fail("Không thể xác nhận đơn.") : Ok(result);
    }

    public async Task<(BookingResponse? Booking, string? Error)> AssignTripAsync(
        int bookingId, AssignTripRequest request, int dispatcherId)
    {
        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is null)
            return Fail("Không tìm thấy đơn.");
        if (booking.Status is BookingStatuses.Completed or BookingStatuses.Cancelled)
            return Fail("Đơn đã kết thúc, không thể phân công.");
        if (booking.Status is not (BookingStatuses.Pending or BookingStatuses.Confirmed))
            return Fail("Đơn không thể phân công.");

        if (request.VehicleId <= 0)
            return Fail("Cần chọn xe.");

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
        if (booking.Status != BookingStatuses.Assigned)
            return Fail("Chỉ giao xe khi đơn đã được gán xe.");
        if (booking.AssignedVehicleId is null)
            return Fail("Đơn chưa được gán xe.");

        var (inspection, inspectError) = await inspections.AddAsync(
            bookingId,
            VehicleInspectionTypes.Handover,
            request?.OdometerKm,
            request?.FuelLevel,
            request?.Condition,
            request?.Notes);
        if (inspection is null)
            return Fail(inspectError ?? "Không thể ghi nhận giao xe.");

        var oldStatus = booking.Status;
        booking.Status = BookingStatuses.InProgress;
        booking.UpdatedAt = DateTime.UtcNow;
        AddHistory(bookingId, oldStatus, BookingStatuses.InProgress, dispatcherId, "Điều phối giao xe tự lái");

        await db.SaveChangesAsync();
        var result = await bookingService.GetBookingByIdAsync(bookingId);
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
        if (booking.Status != BookingStatuses.InProgress)
            return Fail("Chỉ hoàn thành khi đơn đang trong quá trình thuê.");
        if (booking.AssignedVehicleId is null)
            return Fail("Đơn chưa được gán xe.");

        var vehicle = await db.Vehicles.FindAsync(booking.AssignedVehicleId.Value);
        if (vehicle is null)
            return Fail("Không tìm thấy xe đã gán.");

        var handover = await inspections.GetHandoverAsync(bookingId);
        var (inspection, inspectError) = await inspections.AddAsync(
            bookingId,
            VehicleInspectionTypes.Return,
            request?.OdometerKm,
            request?.FuelLevel,
            request?.Condition,
            request?.Notes);
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
        return result is null ? Fail("Không thể hoàn thành đơn.") : Ok(result);
    }

    private async Task<(BookingResponse? Booking, string? Error)> AssignWithDriverAsync(
        Booking booking, AssignTripRequest request, int dispatcherId)
    {
        if (request.DriverId is null or <= 0)
            return Fail("Đơn có tài xế bắt buộc chọn tài xế.");

        if (await db.TripAssignments.AnyAsync(t => t.BookingId == booking.BookingId))
            return Fail("Đơn đã được phân công tài xế và xe.");

        var vehicle = await db.Vehicles
            .FirstOrDefaultAsync(v => v.VehicleId == request.VehicleId && v.Status == VehicleStatuses.Available);
        if (vehicle is null)
            return Fail("Xe không khả dụng.");
        if (vehicle.TypeId != booking.VehicleTypeId)
            return Fail("Xe không thuộc loại xe được đặt.");

        var driver = await db.Drivers
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DriverId == request.DriverId && d.Status == DriverStatuses.Available);
        if (driver is null)
            return Fail("Tài xế không khả dụng.");
        if (await driverService.HasOpenAssignmentAsync(request.DriverId.Value))
            return Fail("Tài xế đang có chuyến chưa hoàn thành.");

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
        driver.Status = DriverStatuses.Busy;
        vehicle.Status = VehicleStatuses.Rented;
        booking.Status = BookingStatuses.Assigned;
        booking.UpdatedAt = DateTime.UtcNow;
        AddHistory(
            booking.BookingId, oldStatus, BookingStatuses.Assigned, dispatcherId,
            $"Phân công tài xế {driver.User.FullName}");

        await db.SaveChangesAsync();
        var result = await bookingService.GetBookingByIdAsync(booking.BookingId);
        return result is null ? Fail("Không thể phân công chuyến đi.") : Ok(result);
    }

    private async Task<(BookingResponse? Booking, string? Error)> AssignSelfDriveAsync(
        Booking booking, AssignTripRequest request, int dispatcherId)
    {
        if (request.DriverId is > 0)
            return Fail("Đơn tự lái không được gán tài xế.");
        if (booking.AssignedVehicleId is not null)
            return Fail("Đơn đã được gán xe.");
        if (await db.TripAssignments.AnyAsync(t => t.BookingId == booking.BookingId))
            return Fail("Đơn đã có phân công chuyến.");

        var vehicle = await db.Vehicles
            .FirstOrDefaultAsync(v => v.VehicleId == request.VehicleId && v.Status == VehicleStatuses.Available);
        if (vehicle is null)
            return Fail("Xe không khả dụng.");
        if (vehicle.TypeId != booking.VehicleTypeId)
            return Fail("Xe không thuộc loại xe được đặt.");

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
        return result is null ? Fail("Không thể gán xe.") : Ok(result);
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

    private static string ResolveMode(string? rentalMode)
        => RentalModes.TryResolve(rentalMode, out var resolved) ? resolved : RentalModes.WithDriver;

    private static (BookingResponse? Booking, string? Error) Ok(BookingResponse booking) => (booking, null);
    private static (BookingResponse? Booking, string? Error) Fail(string error) => (null, error);
}
